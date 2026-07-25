using StServer.Application.DTOs.Option;
using StServer.Application.DTOs.Question;
using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IOptionService
{
    void SyncOptions(Question question, List<OptionUpdateDto> options);
}