using Application.Constants;
using Application.DTOs.Option;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class OptionService : IOptionService
{
    public void SyncOptions(Question question, List<OptionUpdateDto> incoming)
    {
        // Validate everything first, so an invalid request leaves the question untouched
        if (incoming.Count is < QuestionLimits.MinOptions or > QuestionLimits.MaxOptions)
        {
            throw new BadRequestException(
                $"Question must have from {QuestionLimits.MinOptions} to {QuestionLimits.MaxOptions} options");
        }

        var existingById = question.Options.ToDictionary(x => x.Id);

        if (incoming.Any(x => x.Id.HasValue && !existingById.ContainsKey(x.Id.Value)))
        {
            throw new BadRequestException("Option does not belong to this question");
        }

        var names = incoming
            .Select(x => (x.Name ?? existingById.GetValueOrDefault(x.Id ?? Guid.Empty)?.Name)?.Trim())
            .ToList();

        if (names.Any(string.IsNullOrWhiteSpace))
        {
            throw new BadRequestException("Option name is required");
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            throw new BadRequestException("Options must be unique");
        }

        // Missing IsCorrect keeps the current value of an existing option
        var correctFlags = incoming
            .Select(x => x.IsCorrect
                         ?? existingById.GetValueOrDefault(x.Id ?? Guid.Empty)?.IsCorrect
                         ?? false)
            .ToList();

        if (!correctFlags.Any(x => x))
        {
            throw new BadRequestException("At least one correct option required");
        }

        var incomingIds = incoming
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var toRemove = question.Options
            .Where(x => !incomingIds.Contains(x.Id))
            .ToList();

        foreach (var option in toRemove)
        {
            question.Options.Remove(option);
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            var dto = incoming[i];
            Option option;

            if (dto.Id.HasValue)
            {
                option = existingById[dto.Id.Value];
                option.Name = names[i]!;
            }
            else
            {
                option = new Option
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    Name = names[i]!
                };

                question.Options.Add(option);
            }

            option.Position = i;
            option.IsCorrect = correctFlags[i];
        }
    }

    public void SetTrueFalseOptions(Question question, bool isTrue)
    {
        question.Options = new List<Option>
        {
            new()
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Name = QuestionLimits.TrueOptionName,
                Position = 0,
                IsCorrect = isTrue
            },
            new()
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Name = QuestionLimits.FalseOptionName,
                Position = 1,
                IsCorrect = !isTrue
            }
        };
    }

    public void SetCorrectOption(Question question, Guid optionId)
    {
        if (question.Options.All(x => x.Id != optionId))
        {
            throw new BadRequestException("Option does not belong to this question");
        }

        foreach (var option in question.Options)
        {
            option.IsCorrect = option.Id == optionId;
        }
    }
}
