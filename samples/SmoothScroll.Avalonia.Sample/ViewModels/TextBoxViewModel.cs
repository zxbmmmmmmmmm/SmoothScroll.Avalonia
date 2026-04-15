using System;
using System.Linq;

namespace SmoothScroll.Avalonia.Sample.ViewModels;

public class TextBoxViewModel
{
    private const string Lorem = """
        Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.
        """;
    public string Text { get; set; } = string.Join(Environment.NewLine, Enumerable.Range(1, OperatingSystem.IsBrowser() ? 50 : 100).Select(i => $"Line {i}: {Lorem}"));
}
