using System.Text.Json;
using Application.Constants;
using Application.DTOs.Ai;
using Application.DTOs.Material;
using Application.Exceptions;
using Application.Import;
using Application.Interfaces;
using Application.Mappers;
using Domain.Utility.Material;
using Microsoft.Extensions.Logging;
using Application.Observability;

namespace Application.Services;

// "Paste a link, get a material": the article's text becomes a private material of the user,
// questions for it are made right away when asked for. Published text stays the user's own copy
public class MaterialImportService : IMaterialImportService
{
    private const int GeneratedQuestions = 5;

    private readonly IArticleImporter _importer;
    private readonly IMaterialRepository _materials;
    private readonly IAssessmentRepository _assessments;
    private readonly IQuestionGenerationService _questions;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;
    private readonly IContentLimits _limits;
    private readonly ILogger<MaterialImportService> _logger;

    public MaterialImportService(
        IArticleImporter importer,
        IMaterialRepository materials,
        IAssessmentRepository assessments,
        IQuestionGenerationService questions,
        IUserContext user,
        ILimitLock limitLock,
        IContentLimits limits,
        ILogger<MaterialImportService> logger)
    {
        _importer = importer;
        _materials = materials;
        _assessments = assessments;
        _questions = questions;
        _user = user;
        _limitLock = limitLock;
        _limits = limits;
        _logger = logger;
    }

    public async Task<MaterialImportResultDto> ImportAsync(MaterialImportDto dto, CancellationToken cancellationToken = default)
    {
        var userId = _user.UserId;

        // A full account learns it before the page is fetched
        await _limits.EnsureMaterialPlaceAsync(userId, libraryCopy: false);

        ImportedArticle article;
        try
        {
            article = await _importer.ImportAsync(dto.Url ?? "", cancellationToken);
        }
        catch (ArticleImportException exception)
        {
            AppMetrics.ArticleImport(exception.Code.ToString());
            throw;
        }

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Materials, userId);
        await _limits.EnsureMaterialPlaceAsync(userId, libraryCopy: false);

        var material = MaterialMapper.ToEntity(new MaterialCreateDto
        {
            Title = article.Title,
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
        }, userId);
        material.Link = article.Url;
        material.Content = JsonSerializer.SerializeToDocument(article.Document);

        await _materials.AddAsync(material);
        await _assessments.AddAssessmentAsync(AssessmentMapper.ToEntity(material.Id, userId));
        await _materials.SaveChangesAsync();
        await limitLock.CommitAsync();
        AppMetrics.ArticleImport("imported");
        AppMetrics.MaterialCreated("import");

        var (created, skipped) = dto.GenerateQuestions
            ? await GenerateQuestionsAsync(material.Id, cancellationToken)
            : (0, null);

        return new MaterialImportResultDto
        {
            Material = MaterialMapper.ToDto(material),
            QuestionsCreated = created,
            QuestionsSkipped = skipped,
        };
    }

    // The material is kept either way: no quota or no AI only means no questions yet
    private async Task<(int Created, string? Skipped)> GenerateQuestionsAsync(Guid materialId, CancellationToken cancellationToken)
    {
        try
        {
            var questions = await _questions.GenerateAsync(
                materialId, new QuestionGenerateDto { Count = GeneratedQuestions }, cancellationToken);
            return (questions?.Count ?? 0, null);
        }
        catch (AiQuotaExceededException)
        {
            return (0, "quota");
        }
        catch (AiUnavailableException)
        {
            return (0, "unavailable");
        }
        catch (Exception exception) when (exception is AiOutputException or LimitExceededException or BadRequestException)
        {
            _logger.LogInformation(exception, "Questions for an imported material were not made");
            return (0, "failed");
        }
    }
}
