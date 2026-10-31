using Application.DTOs.Option;
using Domain.Entities;

namespace Application.Interfaces;

public interface IOptionService
{
    void SyncOptions(Question question, List<OptionUpdateDto> options);

    void SetTrueFalseOptions(Question question, bool isTrue);

    void SetCorrectOption(Question question, Guid optionId);
}
