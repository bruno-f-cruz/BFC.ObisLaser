using System.Xml;
using Bonsai;
using Bonsai.Expressions;
using Xunit;

namespace BFC.ObisLaser.Tests;

public class WorkflowTests
{
    public static IEnumerable<object[]> Examples() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "examples"), "*.bonsai")
            .Select(path => new object[] { Path.GetFileName(path) });

    [Theory]
    [MemberData(nameof(Examples))]
    public void DocumentationWorkflowsLoadAndBuild(string fileName)
    {
        var workflow = Load(fileName);
        Assert.False(string.IsNullOrWhiteSpace(workflow.Description));
        Assert.NotNull(workflow.Workflow.Build());
    }

    [Fact]
    public void ExamplesAreFound()
    {
        Assert.Equal(6, Examples().Count());
    }

    static WorkflowBuilder Load(string fileName)
    {
        using var reader = XmlReader.Create(Path.Combine(AppContext.BaseDirectory, "examples", fileName));
        return (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
    }
}
