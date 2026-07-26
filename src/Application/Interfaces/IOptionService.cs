using Application.DTOs.Option;
using Application.DTOs.Question;
using Domain.Entities;

namespace Application.Interfaces;

public interface IOptionService
{
    void SyncOptions(Question question, List<OptionUpdateDto> options);
}