using System;
using System.Collections.Generic;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Handlers;

namespace Bimwright.Nwd.Shared.Plugin;

public static partial class NwdCommandRegistry
{
    public static IReadOnlyDictionary<string, INwdCommand> Build(PluginOptions options)
    {
        var dict = new Dictionary<string, INwdCommand>(StringComparer.OrdinalIgnoreCase);
        void Add(INwdCommand cmd) => dict.Add(cmd.Name, cmd);

        Add(new HealthCheckHandler());
        Add(new GetDocumentInfoHandler());
        Add(new GetModelStatisticsHandler());
        Add(new GetModelTreeHandler());
        Add(new GetItemPropertiesHandler());
        Add(new BatchGetPropertiesHandler());
        Add(new FindItemsHandler());
        Add(new FindItemsByNameHandler());
        Add(new GetCurrentSelectionHandler());
        Add(new ClearSelectionHandler());
        Add(new SelectItemsBySearchHandler());
        Add(new ListSetsHandler());
        Add(new GetSelectionSetItemsHandler());
        Add(new ExecuteSearchSetHandler());
        Add(new ListViewpointsHandler());
        Add(new GetCurrentViewpointHandler());
        Add(new GotoViewpointHandler());
        Add(new SaveViewpointHandler());
        Add(new HideItemsHandler());
        Add(new UnhideAllHandler());
        Add(new SendCodeHandler());
        Add(new RunBakedToolHandler());
        Add(new ApplyBakeHandler());

        return dict;
    }
}
