using StServer.Application.DTOs.Option;
using StServer.Application.DTOs.Question;
using StServer.Application.Interfaces;
using StServer.Domain.Entities;

namespace StServer.Application.Services;

public class OptionService : IOptionService
{
    public void SyncOptions(Question question, List<OptionUpdateDto> incoming)
    {
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

        Option? correctOption = null;

        foreach (var dto in incoming)
        {
            Option option;

            if (dto.Id.HasValue)
            {
                option = question.Options.First(x => x.Id == dto.Id);

                option.Name = dto.Name;
            }
            else
            {
                option = new Option
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    Name = dto.Name
                };

                question.Options.Add(option);
            }

            if (dto.IsCorrect)
            {
                correctOption = option;
            }
        }

        if (incoming.Count(x => x.IsCorrect) != 1)
        {
            throw new Exception("At least one correct option required");
        }

        question.CorrectOptionId = correctOption.Id;
    }
}