using System.Globalization;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Particles;

namespace Wagenheimer.MonoGameHelper.Particles;

/// <summary>Loads Ember settings but resolves every texture through the supplied atlas.
/// Stored editor bounds are replaced by current atlas bounds; original pixel size is
/// retained as a draw-scale correction. Never reads a PNG or owns an atlas texture.</summary>
public static class AtlasParticleEffectLoader
{
    public static ParticleEffectInstance Load(Stream stream,Func<string,Texture2DRegion> resolveSprite,int maximumCapacity=8192)
    {
        ArgumentNullException.ThrowIfNull(stream);ArgumentNullException.ThrowIfNull(resolveSprite);
        var document=XDocument.Load(stream);
        if(document.Root?.Name.LocalName!="ParticleEffect")throw new InvalidDataException("Expected an Ember ParticleEffect.");
        var emitters=document.Descendants("ParticleEmitter").ToArray();
        long capacity=0;
        foreach(var emitter in emitters)
        {
            if(!int.TryParse((string?)emitter.Attribute("Capacity"),out var count) || count<1)throw new InvalidDataException("Invalid emitter capacity.");
            capacity+=count;
        }
        if(maximumCapacity<1 || capacity>maximumCapacity)throw new InvalidDataException("Ember effect exceeds the particle budget.");
        var textures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        var savedNames=new Dictionary<Texture2D,string?>();
        var corrections=new Vector2[emitters.Length];
        var regions=new Texture2DRegion[emitters.Length];
        for(int i=0;i<emitters.Length;i++)
        {
            var node=emitters[i].Element("TextureRegion") ?? throw new InvalidDataException("Emitter has no atlas sprite.");
            string name=(string?)node.Attribute("Name")??"";
            string key=Path.GetFileNameWithoutExtension(name.Replace('\\','/'));
            if(string.IsNullOrWhiteSpace(key))throw new InvalidDataException("Emitter sprite name is empty.");
            var region=resolveSprite(key) ?? throw new InvalidDataException($"Atlas sprite not found: {key}");
            var bounds=((string?)node.Attribute("Bounds")??"").Split([' ', ','],StringSplitOptions.RemoveEmptyEntries);
            if(bounds.Length!=4 || !float.TryParse(bounds[2],NumberStyles.Float,CultureInfo.InvariantCulture,out var width) || !float.TryParse(bounds[3],NumberStyles.Float,CultureInfo.InvariantCulture,out var height) || !float.IsFinite(width) || !float.IsFinite(height) || width<=0 || height<=0)
                throw new InvalidDataException("Ember editor texture bounds must be explicit and nonempty.");
            regions[i]=region;
            corrections[i]=new Vector2(width/region.Width,height/region.Height);
            string alias=$"atlas_{i}";
            textures.Add(alias,region.Texture);
            savedNames.TryAdd(region.Texture,region.Texture.Name);
            node.SetAttributeValue("Name",alias);
            node.SetAttributeValue("Bounds",$"{region.X},{region.Y},{region.Width},{region.Height}");
        }
        using var buffer=new MemoryStream();document.Save(buffer);buffer.Position=0;
        using var content=new BorrowedAtlasContent(textures);
        ParticleEffect? effect=null;
        var culture=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            effect=ParticleEffectSerializer.Deserialize(buffer,content,"");
            var scale=new Dictionary<ParticleEmitter,Vector2>();
            for(int i=0;i<effect.Emitters.Count;i++)
            {
                // Assign resolved regions directly too: never trust serialized crop coordinates.
                effect.Emitters[i].TextureRegion=regions[i];
                scale.Add(effect.Emitters[i],corrections[i]);
            }
            return new ParticleEffectInstance(effect,maximumCapacity,scale);
        }
        catch { effect?.Dispose();throw; }
        finally
        {
            CultureInfo.CurrentCulture=culture;
            // Extended's reader assigns Texture.Name. Restore shared atlas metadata.
            foreach(var entry in savedNames)entry.Key.Name=entry.Value;
        }
    }
    private sealed class BorrowedAtlasContent(Dictionary<string,Texture2D> textures) : ContentManager(new EmptyServices())
    {
        public override T Load<T>(string assetName)
        {
            if(typeof(T)==typeof(Texture2D) && textures.TryGetValue(assetName,out var texture))return (T)(object)texture;
            throw new InvalidDataException($"Unexpected particle asset: {assetName}");
        }
    }
    private sealed class EmptyServices : IServiceProvider { public object? GetService(Type type)=>null; }
}
