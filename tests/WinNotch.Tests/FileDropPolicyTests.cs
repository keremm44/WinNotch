using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class FileDropPolicyTests
{
    [Fact]
    public void Evaluate_FileDropWithCopyAllowed_IsAccepted()
    {
        FileDropDecision result = FileDropPolicy.Evaluate(
            moduleEnabled: true,
            draggingOut: false,
            hasFileDropFormat: true,
            copyAllowed: true);

        Assert.True(result.Accepted);
        Assert.Equal(FileDropDecisionReason.Accepted, result.Reason);
    }

    [Theory]
    [InlineData(false, false, true, true, FileDropDecisionReason.ModuleDisabled)]
    [InlineData(true, true, true, true, FileDropDecisionReason.InternalDragOut)]
    [InlineData(true, false, false, true, FileDropDecisionReason.FileDropFormatMissing)]
    [InlineData(true, false, true, false, FileDropDecisionReason.CopyNotAllowed)]
    public void Evaluate_RejectedInputs_ReturnExplicitReason(
        bool moduleEnabled,
        bool draggingOut,
        bool hasFileDropFormat,
        bool copyAllowed,
        FileDropDecisionReason expectedReason)
    {
        FileDropDecision result = FileDropPolicy.Evaluate(
            moduleEnabled,
            draggingOut,
            hasFileDropFormat,
            copyAllowed);

        Assert.False(result.Accepted);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Evaluate_InternalDragOut_WinsOverMissingFormat()
    {
        FileDropDecision result = FileDropPolicy.Evaluate(
            moduleEnabled: true,
            draggingOut: true,
            hasFileDropFormat: false,
            copyAllowed: false);

        Assert.False(result.Accepted);
        Assert.Equal(FileDropDecisionReason.InternalDragOut, result.Reason);
    }
}
