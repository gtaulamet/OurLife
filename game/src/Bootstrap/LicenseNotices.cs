using System.Text;
using Godot;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// The notices an exported game must carry: the Godot license and the copyright and licenses of
/// the third-party parts of the engine, read from the engine itself so they always match the
/// version that ships, and the notice of the .NET runtime the export bundles. The art and fonts
/// of the game are in <c>CREDITS.md</c>, which ships next to this file.
/// </summary>
public static class LicenseNotices
{
    // The .NET runtime is MIT licensed (github.com/dotnet/runtime, LICENSE.TXT).
    private const string DotNetNotice = """
        The MIT License (MIT)

        Copyright (c) .NET Foundation and Contributors

        All rights reserved.

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;

    public static string Build()
    {
        var text = new StringBuilder();
        text.AppendLine("OurLife uses the Godot Engine and the .NET runtime. Their licenses follow.");
        text.AppendLine("The art and fonts of the game are listed in CREDITS.md.");
        Section(text, "Godot Engine");
        text.AppendLine(Engine.GetLicenseText().Trim());

        Section(text, "Third-party components of the Godot Engine");
        foreach (var component in Engine.GetCopyrightInfo())
        {
            text.AppendLine();
            text.AppendLine($"- {component["name"]}");
            foreach (var part in component["parts"].AsGodotArray<Godot.Collections.Dictionary>())
            {
                foreach (var copyright in part["copyright"].AsStringArray())
                {
                    text.AppendLine($"  Copyright (c) {copyright}");
                }

                text.AppendLine($"  License: {part["license"]}");
            }
        }

        Section(text, "Licenses of the third-party components");
        var licenses = Engine.GetLicenseInfo();
        foreach (var name in licenses.Keys.Select(key => key.AsString()).Order(StringComparer.Ordinal))
        {
            text.AppendLine();
            text.AppendLine($"License: {name}");
            text.AppendLine();
            text.AppendLine(licenses[name].AsString().Trim());
        }

        Section(text, ".NET runtime");
        text.AppendLine(DotNetNotice);
        return text.ToString();
    }

    private static void Section(StringBuilder text, string title)
    {
        text.AppendLine();
        text.AppendLine(new string('=', 78));
        text.AppendLine(title);
        text.AppendLine(new string('=', 78));
        text.AppendLine();
    }
}
