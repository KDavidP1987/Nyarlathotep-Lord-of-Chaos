using System.IO;
using System.Reflection;
using Nyarlathotep.Logic;

namespace Nyarlathotep.Services;

/// <summary>
/// The embedded starter templates (event-library D1-D5, D19, D32; third in Core.TryInitialize, after EventStore). The
/// catalogue is read once at boot against the live unit and faction catalogs: "templates: &lt;v&gt;/&lt;n&gt; valid", each
/// invalid template logged with its reason; a missing or unparsable catalogue logs "template catalogue unavailable:
/// &lt;reason&gt;" once, and every template command then replies "template catalogue unavailable" until the next boot.
/// </summary>
internal static class TemplateLibrary
{
    internal static TemplateCatalog Catalog { get; private set; } = TemplateCatalog.NotLoaded;

    /// <summary>Never throws.</summary>
    internal static void Initialize()
    {
        byte[] bytes = null;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateCatalog.ResourceName);
            if (stream is not null)
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }
        }
        catch (Exception ex)
        {
            Catalog = TemplateCatalog.Unavailable($"embedded templates.json could not be read: {ex.Message}");
            Core.Log.LogWarning($"[nyar] template catalogue unavailable: {Catalog.Error}");
            return;
        }
        var units = new EventStore.PrefabUnitCatalog();
        Catalog = TemplateCatalog.Boot(bytes, units, units,
            line => Core.Log.LogInfo($"[nyar] {line}"), line => Core.Log.LogWarning($"[nyar] {line}"));
    }

    /// <summary>`.nyar template use &lt;template&gt; [as &lt;id&gt;]` (D5): appends a disabled copy to events.json.</summary>
    [Mutating]
    internal static string UseTemplate(string template, string asId) =>
        EventStore.Author(text => Authoring.TemplateUse(text, Catalog, template, asId));
}
