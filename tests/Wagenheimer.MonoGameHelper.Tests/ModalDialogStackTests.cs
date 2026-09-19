using Wagenheimer.MonoGameHelper.UI;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class ModalDialogStackTests
{
    private class TestDialog : IModalDialog
    {
        public string DialogName { get; }
        public bool CanCloseWithEscape { get; set; } = true;
        public bool Closed { get; private set; }

        public TestDialog(string name)
        {
            DialogName = name;
        }

        public bool OnCloseRequested()
        {
            Closed = true;
            return true;
        }
    }

    [Fact]
    public void ModalDialogStack_PushAndPop_MaintainsOrder()
    {
        var stack = new ModalDialogStack();
        var d1 = new TestDialog("Menu");
        var d2 = new TestDialog("Settings");

        stack.Push(d1);
        stack.Push(d2);

        Assert.Equal(2, stack.Count);
        Assert.Equal("Settings", stack.ActiveModal?.DialogName);

        var popped = stack.Pop();
        Assert.Equal("Settings", popped?.DialogName);
        Assert.Equal("Menu", stack.ActiveModal?.DialogName);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void ModalDialogStack_TryHandleEscape_ClosesTopDialog()
    {
        var stack = new ModalDialogStack();
        var d1 = new TestDialog("Inventory");
        var d2 = new TestDialog("ConfirmQuit");

        stack.Push(d1);
        stack.Push(d2);

        bool handled = stack.TryHandleEscape();

        Assert.True(handled);
        Assert.True(d2.Closed);
        Assert.False(d1.Closed);
        Assert.Equal(1, stack.Count);
        Assert.Equal("Inventory", stack.ActiveModal?.DialogName);
    }
}
