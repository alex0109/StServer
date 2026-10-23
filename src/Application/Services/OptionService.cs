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

        if (incoming.Count(x => x.IsCorrect is true) != 1)
        {
            throw new BadRequestException("Exactly one correct option required");
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

            if (dto.IsCorrect is true)
            {
                question.CorrectOptionId = option.Id;
            }
        }
    }

    public void SetTrueFalseOptions(Question question, bool isTrue)
    {
        var trueOption = new Option
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Name = QuestionLimits.TrueOptionName,
            Position = 0
        };

        var falseOption = new Option
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Name = QuestionLimits.FalseOptionName,
            Position = 1
        };

        question.Options = new List<Option> { trueOption, falseOption };
        question.CorrectOptionId = isTrue ? trueOption.Id : falseOption.Id;
    }

    public void SetCorrectOption(Question question, Guid optionId)
    {
        if (question.Options.All(x => x.Id != optionId))
        {
            throw new BadRequestException("Option does not belong to this question");
        }

        question.CorrectOptionId = optionId;
    }
}
