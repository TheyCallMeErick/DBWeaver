using SqlEditorFuzzyScorer = DBWeaver.UI.Services.SqlEditor.FuzzyScorer;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class FuzzyScorerTests
{
    [Fact]
    public void Score_FuzzyMatchCreatedAt_ReturnsPositive()
    {
        var sut = new SqlEditorFuzzyScorer();

        int score = sut.Score("crat", "created_at");

        Assert.True(score > 0);
    }

    [Fact]
    public void Score_FuzzyMatchUsers_ReturnsPositive()
    {
        var sut = new SqlEditorFuzzyScorer();

        int score = sut.Score("usrs", "users");

        Assert.True(score > 0);
    }

    [Fact]
    public void Score_NoSubsequence_ReturnsZero()
    {
        var sut = new SqlEditorFuzzyScorer();

        int score = sut.Score("xyz", "users");

        Assert.Equal(0, score);
    }
}
