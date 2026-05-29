using System;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Infrastructure;

public interface INwdCommand
{
    string Name { get; }
    bool IsReadOnly { get; }
    NwdCommandResult Execute(NwdCommandContext context, JObject parameters);
}
