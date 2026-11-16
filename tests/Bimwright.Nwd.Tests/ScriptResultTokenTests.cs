using Bimwright.Nwd.Scripting;
using Bimwright.Nwd.Shared.Handlers;
using Newtonsoft.Json.Linq;

namespace Autodesk.Navisworks.Api.TestFakes
{
    /// <summary>Stands in for a Navisworks API object: it reaches itself, as ModelItem reaches the model tree.</summary>
    public sealed class FakeModelItem
    {
        public string DisplayName => "Wall";
        public FakeModelItem Parent => this;
    }

    public enum FakeUnits { Millimeters, Meters }
}

namespace Bimwright.Nwd.Tests
{
    using Autodesk.Navisworks.Api.TestFakes;

    /// <summary>send_code returns objects as JSON (ipt-mcp ScriptResultToken parity) and refuses Navisworks API objects.</summary>
    public sealed class ScriptResultTokenTests
    {
        private enum Severity { Low, High }

        private sealed class Loop { public Loop? Self { get; set; } }

        private static JToken? Ok(object? value)
        {
            var token = ScriptResultToken.ToResultToken(value, out var error);
            Assert.Null(error);
            return token;
        }

        [Fact]
        public void Anonymous_object_becomes_a_JSON_object()
        {
            var token = Ok(new { title = "A.nwd", count = 3, ids = new[] { "0:1", "0:2" }, nested = new { ok = true } });
            Assert.Equal("""{"title":"A.nwd","count":3,"ids":["0:1","0:2"],"nested":{"ok":true}}""", token!.ToString(Newtonsoft.Json.Formatting.None));
        }

        [Fact]
        public void Task_v1_pass_value_is_JSON_too()
        {
            var pass = McpTaskResult.Pass(new { title = "A.nwd" });
            Assert.Equal("A.nwd", (string?)Ok(SendCodeTask.ResultValue("task_v1", pass))!["title"]);
        }

        [Fact]
        public void Dictionaries_lists_primitives_and_enums()
        {
            Assert.Equal(2, (int)Ok(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 })!["b"]!);
            Assert.Equal(3, ((JArray)Ok(new List<int> { 1, 2, 3 })!).Count);
            Assert.Equal(5, (int)Ok(5)!);
            Assert.Equal("x", (string?)Ok("x"));
            Assert.Equal("High", (string?)Ok(Severity.High));   // by name, as before
            Assert.Null(Ok(null));
            var j = new JObject { ["k"] = 1 };
            Assert.Same(j, Ok(j));
        }

        [Fact]
        public void Navisworks_API_enum_is_a_value_written_by_name()
        {
            Assert.Equal("Meters", (string?)Ok(FakeUnits.Meters));
            Assert.Equal("Meters", (string?)Ok(new { units = FakeUnits.Meters })!["units"]);
        }

        [Fact]
        public void Navisworks_API_object_is_refused()
        {
            Assert.Null(ScriptResultToken.ToResultToken(new FakeModelItem(), out var error));
            Assert.Equal(ScriptResultToken.ApiObjectError, error);
        }

        [Fact]
        public void Nested_Navisworks_API_object_is_refused_before_it_is_walked()
        {
            Assert.Null(ScriptResultToken.ToResultToken(new { name = "x", items = new[] { new FakeModelItem() } }, out var error));
            Assert.Equal(ScriptResultToken.ApiObjectError, error);
        }

        [Fact]
        public void Self_reference_is_refused_with_a_DTO_hint()
        {
            var loop = new Loop();
            loop.Self = loop;
            Assert.Null(ScriptResultToken.ToResultToken(loop, out var error));
            Assert.Contains("not JSON-serializable", error);
            Assert.Contains("DTO", error);
        }
    }
}
