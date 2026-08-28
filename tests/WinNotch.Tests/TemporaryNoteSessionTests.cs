using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class TemporaryNoteSessionTests
{
    [Fact]
    public void Update_RetainsDraftInMemory()
    {
        var note = new TemporaryNoteSession();
        note.Update("toplantı notu");

        Assert.True(note.HasContent);
        Assert.Equal("toplantı notu", note.Text);
    }

    [Fact]
    public void Update_TruncatesUnexpectedlyLargeDraft()
    {
        var note = new TemporaryNoteSession();
        note.Update(new string('x', TemporaryNoteSession.MaxLength + 20));

        Assert.Equal(TemporaryNoteSession.MaxLength, note.Text.Length);
    }

    [Fact]
    public void Clear_RemovesDraft()
    {
        var note = new TemporaryNoteSession();
        note.Update("sil");
        note.Clear();

        Assert.False(note.HasContent);
        Assert.Empty(note.Text);
    }
}
