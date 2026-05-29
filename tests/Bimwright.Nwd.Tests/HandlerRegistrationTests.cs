using System.Linq;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;

namespace Bimwright.Nwd.Tests;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public void EveryCatalogCommandHasExactlyOneHandlerNameAndViceVersa()
    {
        var registered = NwdCommandRegistry.RegisteredNames.OrderBy(x => x).ToArray();
        var catalog = NwdCommandCatalog.All.Select(c => c.Name).OrderBy(x => x).ToArray();
        Assert.Equal(catalog, registered);
    }
}
