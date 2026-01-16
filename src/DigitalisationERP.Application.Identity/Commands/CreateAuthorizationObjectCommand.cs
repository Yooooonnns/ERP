using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class CreateAuthorizationObjectCommand : IRequest<Result<long>>
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string ModuleName { get; set; }

    public CreateAuthorizationObjectCommand(string name, string description, string moduleName)
    {
        Name = name;
        Description = description;
        ModuleName = moduleName;
    }
}
