from pathlib import Path

path = Path("Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnityGraphicsMcpIntegrationHardeningTests.cs")
text = path.read_text(encoding="utf-8")
old = "\t\t\tAssert.That(response, Is.TypeOf<SuccessResponse>());"
new = "\t\t\tAssert.That(response, Is.Not.Null);\n\t\t\tAssert.That(response.GetType().Name, Is.EqualTo(\"SuccessResponse\"));"
if text.count(old) != 1:
    raise SystemExit(f"Expected one response assertion, found {text.count(old)}")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
