using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class BuildCorrectionNoOpPolicyTests
{
    [Fact]
    public void Identical_replacement_is_no_op()
    {
        ValidatedDeveloperProposal proposal = Proposal("class A {}", "class A {}");

        Assert.True(BuildCorrectionNoOpPolicy.IsEntireNoOp(proposal));
        Assert.Equal(["src/a.cs"], BuildCorrectionNoOpPolicy.NoOpPaths(proposal));
    }

    [Fact]
    public void Changed_replacement_is_not_no_op()
    {
        ValidatedDeveloperProposal proposal = Proposal("class A {}", "public class A {}");

        Assert.False(BuildCorrectionNoOpPolicy.IsEntireNoOp(proposal));
        Assert.Empty(BuildCorrectionNoOpPolicy.NoOpPaths(proposal));
    }

    [Fact]
    public void Empty_proposal_does_not_count_as_no_op()
    {
        var proposal = new ValidatedDeveloperProposal("Empty", [], [], [], 0, 0);

        Assert.False(BuildCorrectionNoOpPolicy.IsEntireNoOp(proposal));
    }

    private static ValidatedDeveloperProposal Proposal(string current, string replacement)
    {
        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(current))).ToLowerInvariant();
        return new ValidatedDeveloperProposal(
            "Correction",
            [new ValidatedDeveloperChange(
                DeveloperChangeOperationType.ReplaceFile,
                "src/a.cs", "Correct A", replacement, hash,
                Encoding.UTF8.GetByteCount(replacement))],
            [], [], replacement.Length, 100);
    }
}
