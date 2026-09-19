using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>Canal de animação que o <see cref="SpriteRig"/> sabe avaliar.</summary>
public enum SpriteRigChannel
{
    /// <summary>Posição local em X (unidades do rig).</summary>
    PositionX,

    /// <summary>Posição local em Y (unidades do rig).</summary>
    PositionY,

    /// <summary>Rotação local em Z, em graus.</summary>
    RotationZ,

    /// <summary>Liga/desliga o nó (pose binária: 1 = visível).</summary>
    Active,
}

/// <summary>
/// Uma imagem do rig: a textura **e o retângulo dela** que deve ser desenhado.
///
/// Existe para o rig funcionar tanto com **PNG solto** (retângulo = a textura inteira, ver
/// <see cref="Whole"/>) quanto com **atlas** (retângulo = a área do sprite dentro do atlas). Com
/// atlas, todos os sprites do rig ficam numa textura só — menos troca de textura e o
/// pré-multiplicado vem do loader do atlas.
/// </summary>
public readonly record struct SpriteRigImage(Texture2D Texture, Rectangle SourceRectangle)
{
    /// <summary>Imagem que usa a textura inteira (caso de PNG solto).</summary>
    public static SpriteRigImage Whole(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new SpriteRigImage(texture, new Rectangle(0, 0, texture.Width, texture.Height));
    }

    /// <summary>Largura da imagem, em pixels da textura.</summary>
    public int Width => SourceRectangle.Width;

    /// <summary>Altura da imagem, em pixels da textura.</summary>
    public int Height => SourceRectangle.Height;
}

/// <summary>Um nó da hierarquia: um sprite e/ou o pivô dos filhos.</summary>
public sealed class SpriteRigNode
{
    /// <summary>Nome do nó. Não precisa ser único (nós diferentes podem se chamar <c>bone_1</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Índice do nó pai, ou <c>-1</c> para a raiz.</summary>
    public int Parent { get; init; } = -1;

    /// <summary>Posição local de repouso, em unidades do rig.</summary>
    public Vector2 RestPosition { get; init; }

    /// <summary>Rotação local de repouso em Z, em graus.</summary>
    public float RestRotationZ { get; init; }

    /// <summary>
    /// Ordem de desenho. **Maior desenha depois** (por cima). Empates são resolvidos pela ordem da
    /// hierarquia (o pai antes do filho).
    /// </summary>
    public int Order { get; init; }

    /// <summary>Caminho do sprite, ou <c>null</c> para um nó que só serve de pivô.</summary>
    public string? SpritePath { get; init; }

    /// <summary>
    /// Nó opcional (variante da arte: banner de edição especial, idioma, etc.). Fica escondido a
    /// menos que o chamador peça o contrário em <see cref="SpriteRig.Draw"/>.
    /// </summary>
    public bool Optional { get; init; }
}

/// <summary>Uma curva de animação sobre um canal de um nó.</summary>
public sealed class SpriteRigCurve
{
    /// <summary>Índice do nó animado (em <see cref="SpriteRig.Nodes"/>).</summary>
    public int Node { get; init; } = -1;

    /// <summary>Canal animado.</summary>
    public SpriteRigChannel Channel { get; init; }

    /// <summary>Tempos das chaves, em segundos, crescentes.</summary>
    public float[] Times { get; init; } = Array.Empty<float>();

    /// <summary>Valores correspondentes a <see cref="Times"/>.</summary>
    public float[] Values { get; init; } = Array.Empty<float>();
}

/// <summary>Um clipe: uma "track" com o seu próprio relógio, em loop ou não.</summary>
public sealed class SpriteRigClip
{
    /// <summary>Nome do clipe (usado por <see cref="SpriteRig.FindClipIndex"/>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Nó que serve de raiz para as curvas (é o nó onde o Animator estava, na origem dos dados).
    /// Informativo: as curvas já referenciam os nós por índice.
    /// </summary>
    public int Root { get; init; }

    /// <summary>Duração em segundos. Se for 0, é derivada da última chave das curvas.</summary>
    public float Duration { get; init; }

    /// <summary>Se repete indefinidamente.</summary>
    public bool Loop { get; init; } = true;

    /// <summary>Curvas do clipe.</summary>
    public IReadOnlyList<SpriteRigCurve> Curves { get; init; } = Array.Empty<SpriteRigCurve>();
}

/// <summary>
/// Um "rig" de sprites em camadas, animado por curvas — o port de uma composição animada de
/// sprites (por exemplo, um prefab do Unity com <c>SpriteRenderer</c> + <c>Animator</c>) para
/// MonoGame.
///
/// ## O que é (e o que NÃO é)
///
/// É um **replay de hierarquia de transforms com curvas**: cada sprite é desenhado inteiro, com
/// posição e rotação acumuladas dos pais, na ordem de desenho declarada. Vários clipes rodam em
/// paralelo, cada um no seu relógio.
///
/// **Não** faz deformação de malha (o "SpriteSkin" do Unity 2D Animation): não há vértices nem
/// pesos de osso. Um "osso" aqui é só um pivô — um nó sem sprite. Se você precisar de deformação,
/// o caminho é um runtime esquelético (Spine, DragonBones).
///
/// ## Espaço de coordenadas
///
/// O rig trabalha no espaço da **origem dos dados** (o do prefab do Unity): **Y para CIMA** e
/// rotação em Z anti-horária. A conversão para a tela (Y para baixo, rotação horária) acontece em
/// <see cref="Draw"/>, a partir do <paramref name="origin"/> informado. O pivô de cada sprite é o
/// **centro**.
///
/// ## Como usar
///
/// <code>
/// var rig = SpriteRig.FromJson(File.ReadAllText("Content/Data/Logo/logo_frame.json"));
/// rig.AssignImages(path => MyAtlas.Resolve(path));   // ver a regra de atlas no AGENTS.md
///
/// // por frame
/// rig.Update(deltaSeconds);
/// rig.Draw(spriteBatch, origin: designCenter + new Vector2(0, -250f));
/// </code>
///
/// Também é possível montar o rig **em código**, sem JSON (ver <see cref="Create"/>).
///
/// ## Robustez
///
/// A construção valida tudo e falha cedo com <see cref="InvalidDataException"/> e mensagem útil:
/// pai inexistente, pai depois do filho (o que também descarta ciclos), curva apontando para nó
/// inválido, tempos/valores de tamanhos diferentes, valores não finitos e canais desconhecidos.
/// As chaves são ordenadas por tempo no carregamento, então JSON feito à mão fora de ordem
/// funciona. Nada é alocado em <see cref="Update"/> nem em <see cref="Draw"/>.
/// </summary>
public sealed class SpriteRig
{
    private readonly SpriteRigNode[] _nodes;
    private readonly SpriteRigClip[] _clips;
    private readonly SpriteRigCurve[][] _clipCurves;
    private readonly int[] _spriteIndexByNode;
    private readonly int[] _drawOrder;
    private readonly string[] _spritePaths;

    private readonly Vector2[] _localPosition;
    private readonly float[] _localRotationZ;
    private readonly bool[] _visible;
    private readonly Vector2[] _worldPosition;
    private readonly float[] _worldRotationZ;
    private readonly float[] _clock;

    /// <summary>Imagens dos sprites, paralelas a <see cref="SpritePaths"/>.</summary>
    public SpriteRigImage?[] Images { get; private set; }

    /// <summary>Multiplicador de tempo aplicado a <see cref="Update"/> (1 = normal, 0 = pausado).</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>
    /// Escala **base** de todo o rig: 1 quando a arte é 1x, `1 / ArtScale` quando os sprites vêm de
    /// um atlas 2x/4x (o retângulo dobra, mas o tamanho de design não muda — ver a regra de
    /// multi-resolução no `AGENTS.md`).
    ///
    /// É aplicada em <see cref="Draw"/> e em <see cref="GetWorldPosition(int)"/> (que devolve a
    /// posição já em unidades de design).
    /// </summary>
    public float BaseScale { get; set; } = 1f;

    /// <summary>Nós do rig, em ordem depth-first (o pai vem sempre antes dos filhos).</summary>
    public IReadOnlyList<SpriteRigNode> Nodes => _nodes;

    /// <summary>Clipes de animação.</summary>
    public IReadOnlyList<SpriteRigClip> Clips => _clips;

    /// <summary>Caminhos distintos dos sprites, na ordem em que são carregados.</summary>
    public IReadOnlyList<string> SpritePaths => _spritePaths;

    /// <summary>
    /// Índices dos nós em **ordem de desenho** (do fundo para a frente), já com os empates
    /// resolvidos pela hierarquia. É o que um renderizador próprio (que não use
    /// <see cref="Draw"/>) precisa percorrer para respeitar a ordem.
    /// </summary>
    public IReadOnlyList<int> DrawOrder => _drawOrder;

    /// <summary>Duração do clipe mais longo, em segundos.</summary>
    public float Duration
    {
        get
        {
            float max = 0f;
            for (int i = 0; i < _clips.Length; i++)
                if (_clips[i].Duration > max)
                    max = _clips[i].Duration;
            return max;
        }
    }

    private SpriteRig(
        SpriteRigNode[] nodes,
        SpriteRigClip[] clips,
        SpriteRigCurve[][] clipCurves,
        string[] spritePaths,
        int[] spriteIndexByNode)
    {
        _nodes = nodes;
        _clips = clips;
        _clipCurves = clipCurves;
        _spritePaths = spritePaths;
        _spriteIndexByNode = spriteIndexByNode;

        _localPosition = new Vector2[nodes.Length];
        _localRotationZ = new float[nodes.Length];
        _visible = new bool[nodes.Length];
        _worldPosition = new Vector2[nodes.Length];
        _worldRotationZ = new float[nodes.Length];
        _clock = new float[clips.Length];

        // Empate de ordem resolvido pelo índice, que desempata de forma determinística e (porque os
        // nós vêm em depth-first) mantém o filho depois do pai — é o que põe a pálpebra sobre o
        // rosto e a aboborinha sobre a barra.
        _drawOrder = new int[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
            _drawOrder[i] = i;

        Array.Sort(_drawOrder, (a, b) =>
        {
            int cmp = nodes[a].Order.CompareTo(nodes[b].Order);
            return cmp != 0 ? cmp : a.CompareTo(b);
        });

        Images = new SpriteRigImage?[spritePaths.Length];
        Reset();
    }

    /// <summary>
    /// Monta um rig a partir de nós e clipes já em memória (sem passar por JSON).
    /// </summary>
    /// <exception cref="InvalidDataException">Estrutura inconsistente (ver os Remarks da classe).</exception>
    public static SpriteRig Create(
        IReadOnlyList<SpriteRigNode> nodes,
        IReadOnlyList<SpriteRigClip> clips)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(clips);

        if (nodes.Count == 0)
            throw new InvalidDataException("O rig precisa de pelo menos um nó.");

        var nodeArray = new SpriteRigNode[nodes.Count];
        for (int i = 0; i < nodeArray.Length; i++)
            nodeArray[i] = nodes[i] ?? throw new InvalidDataException($"O nó {i} é nulo.");

        // Caminhos distintos de sprite (na ordem de primeira aparição) + índice por nó.
        var paths = new List<string>();
        var pathIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var spriteIndexByNode = new int[nodeArray.Length];

        for (int i = 0; i < nodeArray.Length; i++)
        {
            var node = nodeArray[i];

            if (node.Parent < -1)
                throw new InvalidDataException(
                    $"Nó {i} ('{node.Name}'): pai {node.Parent} é inválido (use -1 para a raiz).");

            if (node.Parent >= i)
                throw new InvalidDataException(
                    $"Nó {i} ('{node.Name}'): o pai ({node.Parent}) precisa vir antes do filho — " +
                    "os nós são avaliados em ordem e isso também garante que não há ciclo.");

            if (!IsFinite(node.RestPosition.X) || !IsFinite(node.RestPosition.Y) || !IsFinite(node.RestRotationZ))
                throw new InvalidDataException($"Nó {i} ('{node.Name}'): posição/rotação de repouso não é finita.");

            if (string.IsNullOrEmpty(node.SpritePath))
            {
                spriteIndexByNode[i] = -1;
                continue;
            }

            if (!pathIndex.TryGetValue(node.SpritePath, out int idx))
            {
                idx = paths.Count;
                paths.Add(node.SpritePath);
                pathIndex[node.SpritePath] = idx;
            }

            spriteIndexByNode[i] = idx;
        }

        var clipArray = new SpriteRigClip[clips.Count];
        var curveArray = new SpriteRigCurve[clips.Count][];

        for (int c = 0; c < clipArray.Length; c++)
        {
            var clip = clips[c] ?? throw new InvalidDataException($"O clipe {c} é nulo.");
            clipArray[c] = clip;

            var source = clip.Curves;
            if (source == null || source.Count == 0)
            {
                curveArray[c] = Array.Empty<SpriteRigCurve>();
                continue;
            }

            var curves = new SpriteRigCurve[source.Count];
            float lastKey = 0f;

            for (int k = 0; k < curves.Length; k++)
            {
                var curve = source[k] ?? throw new InvalidDataException(
                    $"Clipe '{clip.Name}': a curva {k} é nula.");

                if (curve.Node < 0 || curve.Node >= nodeArray.Length)
                    throw new InvalidDataException(
                        $"Clipe '{clip.Name}', curva {k}: aponta para o nó {curve.Node}, " +
                        $"que não existe (o rig tem {nodeArray.Length}).");

                var times = curve.Times;
                var values = curve.Values;

                if (times == null || values == null || times.Length == 0)
                    throw new InvalidDataException($"Clipe '{clip.Name}', curva {k}: sem chaves.");

                if (times.Length != values.Length)
                    throw new InvalidDataException(
                        $"Clipe '{clip.Name}', curva {k}: {times.Length} tempos para {values.Length} valores.");

                for (int t = 0; t < times.Length; t++)
                {
                    if (!IsFinite(times[t]) || !IsFinite(values[t]))
                        throw new InvalidDataException(
                            $"Clipe '{clip.Name}', curva {k}, chave {t}: valor não finito.");

                    if (t > 0 && times[t] < times[t - 1])
                    {
                        // Aceita JSON feito à mão fora de ordem: ordena por tempo (as chaves são
                        // poucas e isto roda uma vez só).
                        SortKeys(times, values);
                        break;
                    }
                }

                if (times[times.Length - 1] > lastKey)
                    lastKey = times[times.Length - 1];

                curves[k] = curve;
            }

            curveArray[c] = curves;

            // Uma duração ausente (0) é derivada das curvas, em vez de travar a animação.
            if (clip.Duration <= 0f && lastKey > 0f)
            {
                clipArray[c] = new SpriteRigClip
                {
                    Name = clip.Name,
                    Root = clip.Root,
                    Duration = lastKey,
                    Loop = clip.Loop,
                    Curves = clip.Curves,
                };
            }
        }

        return new SpriteRig(nodeArray, clipArray, curveArray, paths.ToArray(), spriteIndexByNode);
    }

    /// <summary>Carrega o rig de um JSON (ver o schema no README do pacote).</summary>
    /// <exception cref="InvalidDataException">JSON malformado ou estrutura inconsistente.</exception>
    public static SpriteRig FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON inválido: {ex.Message}", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            var nodes = new List<SpriteRigNode>();
            foreach (var e in RequireArray(root, "nodes").EnumerateArray())
            {
                nodes.Add(new SpriteRigNode
                {
                    Name = ReadString(e, "name") ?? string.Empty,
                    Parent = ReadInt(e, "parent", -1),
                    RestPosition = ReadVector2(e, "pos"),
                    RestRotationZ = ReadFloat(e, "rotZ", 0f),
                    Order = ReadInt(e, "order", 0),
                    SpritePath = ReadString(e, "sprite"),
                    // "ceOnly" é o nome legado (Collector's Edition) — aceito por compatibilidade.
                    Optional = ReadBool(e, "optional", false) || ReadBool(e, "ceOnly", false),
                });
            }

            var clips = new List<SpriteRigClip>();
            foreach (var c in RequireArray(root, "clips").EnumerateArray())
            {
                var curves = new List<SpriteRigCurve>();
                foreach (var cur in RequireArray(c, "curves").EnumerateArray())
                {
                    var keys = RequireArray(cur, "keys");
                    var times = new float[keys.GetArrayLength()];
                    var values = new float[times.Length];

                    for (int k = 0; k < times.Length; k++)
                    {
                        var key = keys[k];
                        if (key.ValueKind != JsonValueKind.Array || key.GetArrayLength() < 2)
                            throw new InvalidDataException(
                                $"Clipe '{ReadString(c, "name")}', chave {k}: esperado [tempo, valor].");

                        times[k] = key[0].GetSingle();
                        values[k] = key[1].GetSingle();
                    }

                    curves.Add(new SpriteRigCurve
                    {
                        Node = ReadInt(cur, "node", -1),
                        Channel = ParseChannel(ReadString(cur, "channel")),
                        Times = times,
                        Values = values,
                    });
                }

                clips.Add(new SpriteRigClip
                {
                    Name = ReadString(c, "name") ?? string.Empty,
                    Root = ReadInt(c, "root", 0),
                    Duration = ReadFloat(c, "duration", 0f),
                    Loop = ReadBool(c, "loop", true),
                    Curves = curves,
                });
            }

            return Create(nodes, clips);
        }
    }

    /// <summary>Carrega o rig de um stream (útil para recursos embarcados).</summary>
    public static SpriteRig Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    /// <summary>
    /// Registra as imagens dos sprites (textura + recorte). <paramref name="imageForPath"/> recebe
    /// cada item de <see cref="SpritePaths"/> e pode devolver <c>null</c> para pular aquele sprite
    /// (o nó deixa de desenhar, sem erro).
    ///
    /// Este é o caminho para **atlas**: devolva o recorte do sprite dentro da textura empacotada.
    /// </summary>
    public void AssignImages(Func<string, SpriteRigImage?> imageForPath)
    {
        ArgumentNullException.ThrowIfNull(imageForPath);

        var images = new SpriteRigImage?[_spritePaths.Length];
        for (int i = 0; i < images.Length; i++)
            images[i] = imageForPath(_spritePaths[i]);

        Images = images;
    }

    /// <summary>
    /// Atalho para **PNG solto**: cada caminho vira a textura inteira.
    /// Prefira <see cref="AssignImages"/> com um atlas — ver a regra de atlas no `AGENTS.md`.
    /// </summary>
    public void AssignTextures(Func<string, Texture2D?> textureForPath)
    {
        ArgumentNullException.ThrowIfNull(textureForPath);

        AssignImages(path =>
        {
            var texture = textureForPath(path);
            return texture == null ? null : SpriteRigImage.Whole(texture);
        });
    }

    /// <summary>Volta à pose de repouso e zera todos os relógios.</summary>
    public void Reset()
    {
        Array.Clear(_clock, 0, _clock.Length);
        ApplyRestPose();
    }

    private void ApplyRestPose()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            _localPosition[i] = _nodes[i].RestPosition;
            _localRotationZ[i] = _nodes[i].RestRotationZ;
            _visible[i] = true;
        }

        ComposeWorld();
    }

    /// <summary>Avança os clipes em <paramref name="deltaSeconds"/> e recompõe a hierarquia.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaSeconds"/> negativo.</exception>
    public void Update(float deltaSeconds)
    {
        if (deltaSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "O tempo não anda para trás.");

        Advance(deltaSeconds);
    }

    private void Advance(float deltaSeconds)
    {
        // 1. Repouso + clipes. Cada clipe anima só os seus canais; o que ninguém anima mantém o
        //    valor do prefab.
        ApplyRestPoseValues();

        float scaled = deltaSeconds * TimeScale;

        for (int c = 0; c < _clips.Length; c++)
        {
            var clip = _clips[c];
            var curves = _clipCurves[c];

            if (clip.Duration <= 0f || curves.Length == 0)
                continue;

            float t = _clock[c] + scaled;
            t = clip.Loop ? t % clip.Duration : MathF.Min(t, clip.Duration);
            _clock[c] = t;

            for (int k = 0; k < curves.Length; k++)
                Apply(curves[k], t);
        }

        ComposeWorld();
    }

    private void ApplyRestPoseValues()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            _localPosition[i] = _nodes[i].RestPosition;
            _localRotationZ[i] = _nodes[i].RestRotationZ;
            _visible[i] = true;
        }
    }

    private void ComposeWorld()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            int p = _nodes[i].Parent;
            if (p < 0)
            {
                _worldPosition[i] = _localPosition[i];
                _worldRotationZ[i] = _localRotationZ[i];
            }
            else
            {
                _worldPosition[i] = _worldPosition[p] + Rotate(_localPosition[i], _worldRotationZ[p]);
                _worldRotationZ[i] = _worldRotationZ[p] + _localRotationZ[i];
            }
        }
    }

    private void Apply(SpriteRigCurve curve, float time)
    {
        int node = curve.Node;
        float value = Evaluate(curve, time);

        switch (curve.Channel)
        {
            case SpriteRigChannel.PositionX:
                _localPosition[node].X = value;
                break;
            case SpriteRigChannel.PositionY:
                _localPosition[node].Y = value;
                break;
            case SpriteRigChannel.RotationZ:
                _localRotationZ[node] = value;
                break;
            case SpriteRigChannel.Active:
                _visible[node] = value >= 0.5f;
                break;
        }
    }

    /// <summary>
    /// Avalia a curva. Canais de pose (<see cref="SpriteRigChannel.Active"/>) usam **degrau** — uma
    /// piscada precisa ligar/desligar, não interpolar. Os demais usam suavização (ease in/out), que
    /// é o comportamento das tangentes "Auto" do Unity num movimento de duas poses (sobe e volta).
    /// </summary>
    private static float Evaluate(SpriteRigCurve curve, float time)
    {
        var times = curve.Times;
        var values = curve.Values;

        if (time <= times[0])
            return values[0];

        int last = times.Length - 1;
        if (time >= times[last])
            return values[last];

        int i = 1;
        while (i < last && times[i] < time)
            i++;

        float t0 = times[i - 1];
        float t1 = times[i];
        float span = t1 - t0;
        if (span <= 0f)
            return values[i];

        if (curve.Channel == SpriteRigChannel.Active)
            return values[i - 1];   // degrau: mantém o valor da chave anterior

        float u = (time - t0) / span;
        u = u * u * (3f - 2f * u);  // smoothstep
        return MathHelper.Lerp(values[i - 1], values[i], u);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        if (degrees == 0f)
            return v;

        float rad = MathHelper.ToRadians(degrees);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    /// <summary>
    /// Desenha o rig. As coordenadas são as do rig (Y para cima); a conversão para a tela
    /// (Y para baixo e rotação horária) acontece aqui, a partir do <paramref name="origin"/>.
    /// </summary>
    /// <param name="spriteBatch">Batch já iniciado, no espaço em que <paramref name="origin"/> vive.</param>
    /// <param name="origin">Posição do pivô do rig (a raiz) no espaço do batch.</param>
    /// <param name="scale">Escala uniforme aplicada a posições e tamanhos.</param>
    /// <param name="opacity">Opacidade global (0 a 1).</param>
    /// <param name="showOptional">Inclui os nós opcionais (variantes de edição/idioma).</param>
    public void Draw(
        SpriteBatch spriteBatch,
        Vector2 origin,
        float scale = 1f,
        float opacity = 1f,
        bool showOptional = false)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);

        if (opacity <= 0f)
            return;

        var color = Color.White * opacity;

        for (int d = 0; d < _drawOrder.Length; d++)
        {
            int i = _drawOrder[d];
            int spriteIndex = _spriteIndexByNode[i];

            if (spriteIndex < 0 || !_visible[i])
                continue;

            var node = _nodes[i];
            if (node.Optional && !showOptional)
                continue;

            if (spriteIndex >= Images.Length)
                continue;

            var image = Images[spriteIndex];
            if (image == null)
                continue;

            var texture = image.Value.Texture;
            if (texture == null || texture.IsDisposed)
                continue;

            var source = image.Value.SourceRectangle;
            var world = _worldPosition[i];
            float effectiveScale = scale * BaseScale;

            spriteBatch.Draw(
                texture,
                new Vector2(origin.X + world.X * effectiveScale, origin.Y - world.Y * effectiveScale),
                source,
                color,
                -MathHelper.ToRadians(_worldRotationZ[i]),
                // O pivô é o CENTRO da imagem (do recorte, não da textura): com atlas, o recorte é a
                // área do sprite dentro da textura empacotada.
                new Vector2(source.Width / 2f, source.Height / 2f),
                effectiveScale,
                SpriteEffects.None,
                0f);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Consultas (para depuração, testes e para ancorar outras coisas num nó, ex.: uma partícula)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Acha o índice de um nó pelo nome. Nomes podem se repetir no rig (ex.: <c>bone_1</c> em dois
    /// personagens), então devolve o **primeiro** que casa; prefira guardar o índice.
    /// </summary>
    public bool TryGetNodeIndex(string nodeName, out int index)
    {
        if (!string.IsNullOrEmpty(nodeName))
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (string.Equals(_nodes[i].Name, nodeName, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
        }

        index = -1;
        return false;
    }

    /// <summary>
    /// Posição de um nó no espaço do rig, já com a animação aplicada e escalada por
    /// <see cref="BaseScale"/> (ou seja, em unidades de design).
    /// </summary>
    public Vector2 GetWorldPosition(int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);
        return _worldPosition[nodeIndex] * BaseScale;
    }

    /// <summary>
    /// Imagem registrada para um nó (textura + recorte), ou <c>null</c> se o nó é só um pivô, se a
    /// imagem não foi registrada ou se a textura foi descartada.
    ///
    /// Serve para renderizadores próprios que percorrem <see cref="DrawOrder"/> — por exemplo, um
    /// componente do engine que desenhe com o seu próprio batcher.
    /// </summary>
    public SpriteRigImage? GetNodeImage(int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);

        int spriteIndex = _spriteIndexByNode[nodeIndex];
        if (spriteIndex < 0 || spriteIndex >= Images.Length)
            return null;

        var image = Images[spriteIndex];
        if (image == null)
            return null;

        var texture = image.Value.Texture;
        return texture == null || texture.IsDisposed ? null : image;
    }

    /// <summary>Rotação de um nó no espaço do rig, em graus.</summary>
    public float GetWorldRotationZ(int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);
        return _worldRotationZ[nodeIndex];
    }

    /// <summary>Verdadeiro se o nó está visível na pose atual (canal <see cref="SpriteRigChannel.Active"/>).</summary>
    public bool IsNodeVisible(int nodeIndex)
    {
        ValidateNodeIndex(nodeIndex);
        return _visible[nodeIndex];
    }

    /// <summary>Posição de um nó pelo nome (primeiro que casar). Ver <see cref="TryGetNodeIndex"/>.</summary>
    public Vector2 GetWorldPosition(string nodeName) => GetWorldPosition(RequireNodeIndex(nodeName));

    /// <summary>Rotação de um nó pelo nome (primeiro que casar), em graus.</summary>
    public float GetWorldRotationZ(string nodeName) => GetWorldRotationZ(RequireNodeIndex(nodeName));

    /// <summary>Visibilidade de um nó pelo nome (primeiro que casar).</summary>
    public bool IsNodeVisible(string nodeName) => IsNodeVisible(RequireNodeIndex(nodeName));

    /// <summary>Índice de um clipe pelo nome, ou <c>-1</c>.</summary>
    public int FindClipIndex(string clipName)
    {
        if (!string.IsNullOrEmpty(clipName))
        {
            for (int i = 0; i < _clips.Length; i++)
            {
                if (string.Equals(_clips[i].Name, clipName, StringComparison.Ordinal))
                    return i;
            }
        }

        return -1;
    }

    /// <summary>Relógio atual de um clipe, em segundos.</summary>
    public float GetClipTime(int clipIndex)
    {
        ValidateClipIndex(clipIndex);
        return _clock[clipIndex];
    }

    /// <summary>Reposiciona o relógio de um clipe (ex.: sincronizar duas camadas).</summary>
    public void SetClipTime(int clipIndex, float seconds)
    {
        ValidateClipIndex(clipIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);

        var clip = _clips[clipIndex];
        _clock[clipIndex] = clip.Loop && clip.Duration > 0f
            ? seconds % clip.Duration
            : seconds;
    }

    /// <summary>Relógio atual de um clipe pelo nome.</summary>
    public float GetClipTime(string clipName) => GetClipTime(RequireClipIndex(clipName));

    /// <summary>Reposiciona o relógio de um clipe pelo nome.</summary>
    public void SetClipTime(string clipName, float seconds) => SetClipTime(RequireClipIndex(clipName), seconds);

    private int RequireNodeIndex(string nodeName)
    {
        if (!TryGetNodeIndex(nodeName, out int index))
            throw new ArgumentException($"Nó '{nodeName}' não existe no rig.", nameof(nodeName));

        return index;
    }

    private int RequireClipIndex(string clipName)
    {
        int index = FindClipIndex(clipName);
        if (index < 0)
            throw new ArgumentException($"Clipe '{clipName}' não existe no rig.", nameof(clipName));

        return index;
    }

    private void ValidateNodeIndex(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _nodes.Length)
            throw new ArgumentOutOfRangeException(
                nameof(nodeIndex), nodeIndex, $"O rig tem {_nodes.Length} nós.");
    }

    private void ValidateClipIndex(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= _clips.Length)
            throw new ArgumentOutOfRangeException(
                nameof(clipIndex), clipIndex, $"O rig tem {_clips.Length} clipes.");
    }

    // -----------------------------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------------------------

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    /// <summary>Ordena chaves por tempo (insertion sort: são poucas e é chamado uma vez).</summary>
    private static void SortKeys(float[] times, float[] values)
    {
        for (int i = 1; i < times.Length; i++)
        {
            float t = times[i];
            float v = values[i];
            int j = i - 1;

            while (j >= 0 && times[j] > t)
            {
                times[j + 1] = times[j];
                values[j + 1] = values[j];
                j--;
            }

            times[j + 1] = t;
            values[j + 1] = v;
        }
    }

    private static SpriteRigChannel ParseChannel(string? name) => name switch
    {
        "posX" => SpriteRigChannel.PositionX,
        "posY" => SpriteRigChannel.PositionY,
        "rotZ" => SpriteRigChannel.RotationZ,
        "active" => SpriteRigChannel.Active,
        _ => throw new InvalidDataException(
            $"Canal desconhecido: '{name}'. Use posX, posY, rotZ ou active."),
    };

    private static JsonElement RequireArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Campo '{name}' ausente ou não é uma lista.");

        return value;
    }

    private static string? ReadString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static float ReadFloat(JsonElement e, string name, float fallback)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : fallback;

    private static int ReadInt(JsonElement e, string name, int fallback)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    private static bool ReadBool(JsonElement e, string name, bool fallback)
        => e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : fallback;

    private static Vector2 ReadVector2(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array || v.GetArrayLength() < 2)
            throw new InvalidDataException($"Campo '{name}' ausente ou não é [x, y].");

        return new Vector2(v[0].GetSingle(), v[1].GetSingle());
    }
}
