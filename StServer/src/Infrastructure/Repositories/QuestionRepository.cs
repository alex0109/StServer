using Microsoft.EntityFrameworkCore;
using StServer.Application.Interfaces;
using StServer.Domain.Entities;
using StServer.Infrastructure.Data;

namespace StServer.Infrastructure.Repositories;

public class QuestionRepository : IQuestionRepository
{
    private readonly AppDbContext _db;

    public QuestionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Question>> GetAllQuestionsAsync(Guid materialId, Guid userId)
    {
        return await _db.Questions
            .Include(q => q.Options)
            .Where(x => x.MaterialId == materialId && x.UserId == userId)
            .ToListAsync();
    }
    
    public async Task<Question?> GetByIdQuestionAsync(Guid materialId, Guid id, Guid userId)
    {
        return await _db.Questions
            .Include(m => m.Options)
            .FirstOrDefaultAsync(x => x.MaterialId == materialId && x.Id == id && x.UserId == userId);
    }
    
    public async Task<Question> AddQuestionAsync(Question question)
    {
        await _db.Questions.AddAsync(question);
        return question;
    }
    
    public async Task<bool> DeleteQuestionAsync(Guid materialId, Guid id, Guid userId)
    {
        var question = await _db.Questions
            .FirstOrDefaultAsync(x => x.MaterialId == materialId && x.Id == id && x.UserId == userId);
        
        if (question is null)
        {
            return false;
        }
        
        _db.Questions.Remove(question);
        return true;
        
    }
    
    public async Task<int> CountByMaterialIdAsync(Guid materialId)
    {
        return await _db.Questions
            .CountAsync(x => x.MaterialId == materialId);
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}