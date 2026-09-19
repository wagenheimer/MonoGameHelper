using System;
using System.IO;
using System.Text;
using Wagenheimer.MonoGameHelper.Graphics;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

/// <summary>
/// Testes do <see cref="SpriteRig"/>: composição da hierarquia, avaliação das curvas, canais de
/// pose, validação dos dados e controle de clipes.
///
/// Nada aqui precisa de <c>GraphicsDevice</c>: o rig só toca a GPU em <see cref="SpriteRig.Draw"/>,
/// então toda a matemática e toda a validação são testáveis sem janela.
/// </summary>
public class SpriteRigTests
{
    // ---------------------------------------------------------------------------------------
    // Helpers de montagem (o rig também pode ser construído em código, sem JSON)
    // ---------------------------------------------------------------------------------------

    private static SpriteRigNode Node(
        string name, int parent = -1, float x = 0f, float y = 0f, float rotZ = 0f,
        int order = 0, string? sprite = null, bool optional = false)
        => new()
        {
            Name = name,
            Parent = parent,
            RestPosition = new Microsoft.Xna.Framework.Vector2(x, y),
            RestRotationZ = rotZ,
            Order = order,
            SpritePath = sprite,
            Optional = optional,
        };

    private static SpriteRigCurve Curve(int node, string channel, params (float T, float V)[] keys)
    {
        var times = new float[keys.Length];
        var values = new float[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            times[i] = keys[i].T;
            values[i] = keys[i].V;
        }

        return new SpriteRigCurve
        {
            Node = node,
            Channel = channel switch
            {
                "posX" => SpriteRigChannel.PositionX,
                "posY" => SpriteRigChannel.PositionY,
                "rotZ" => SpriteRigChannel.RotationZ,
                "active" => SpriteRigChannel.Active,
                _ => throw new ArgumentException($"canal de teste inválido: {channel}"),
            },
            Times = times,
            Values = values,
        };
    }

    private static SpriteRigClip Clip(string name, int root, float duration, bool loop, params SpriteRigCurve[] curves)
        => new() { Name = name, Root = root, Duration = duration, Loop = loop, Curves = curves };

    /// <summary>Rig mínimo: raiz -> bone (90 graus) -> folha deslocada em X.</summary>
    private static SpriteRig FoldedChain() => SpriteRig.Create(
        new[]
        {
            Node("root"),
            Node("bone", parent: 0, rotZ: 90f),
            Node("leaf", parent: 1, x: 10f, sprite: "a.png"),
        },
        Array.Empty<SpriteRigClip>());

    // ---------------------------------------------------------------------------------------
    // Hierarquia e espaço
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void FoldedParent_RotatesChildOffsetIntoWorldY()
    {
        var rig = FoldedChain();
        rig.Update(0f);

        var leaf = rig.GetWorldPosition("leaf");

        Assert.Equal(0f, leaf.X, 3);
        Assert.Equal(10f, leaf.Y, 3);
    }

    [Fact]
    public void RotationsAccumulateDownTheChain()
    {
        var rig = FoldedChain();
        rig.Update(0f);

        Assert.Equal(90f, rig.GetWorldRotationZ("leaf"), 3);
    }

    [Fact]
    public void RestPose_IsValidBeforeTheFirstUpdate()
    {
        // O construtor já compõe o mundo: dá para consultar antes de qualquer Update.
        var rig = FoldedChain();

        Assert.Equal(10f, rig.GetWorldPosition("leaf").Y, 3);
    }

    [Fact]
    public void SpritePath_IsDeduplicatedAcrossNodes()
    {
        var rig = SpriteRig.Create(
            new[]
            {
                Node("a", sprite: "same.png"),
                Node("b", sprite: "same.png"),
                Node("c", sprite: "other.png"),
                Node("pivot"),
            },
            Array.Empty<SpriteRigClip>());

        Assert.Equal(2, rig.SpritePaths.Count);
        Assert.Equal("same.png", rig.SpritePaths[0]);
        Assert.Equal("other.png", rig.SpritePaths[1]);
    }

    // ---------------------------------------------------------------------------------------
    // Validação (falha cedo, com mensagem útil)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void EmptyRig_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(Array.Empty<SpriteRigNode>(), Array.Empty<SpriteRigClip>()));

    [Fact]
    public void ParentAfterChild_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("leaf", parent: 1), Node("root") },
            Array.Empty<SpriteRigClip>()));

    [Fact]
    public void ParentBelowMinusOne_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("root", parent: -7) },
            Array.Empty<SpriteRigClip>()));

    [Fact]
    public void NonFiniteRestPose_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("root", x: float.NaN) },
            Array.Empty<SpriteRigClip>()));

    [Fact]
    public void CurvePointingToMissingNode_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("root") },
            new[] { Clip("c", 0, 1f, true, Curve(5, "posX", (0f, 0f))) }));

    [Fact]
    public void CurveWithMismatchedKeyArrays_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("root") },
            new[]
            {
                Clip("c", 0, 1f, true, new SpriteRigCurve
                {
                    Node = 0,
                    Channel = SpriteRigChannel.PositionX,
                    Times = new[] { 0f, 1f },
                    Values = new[] { 0f },
                }),
            }));

    [Fact]
    public void CurveWithoutKeys_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("root") },
            new[] { Clip("c", 0, 1f, true, new SpriteRigCurve { Node = 0, Channel = SpriteRigChannel.PositionX }) }));

    [Fact]
    public void CurveWithNonFiniteValue_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.Create(
            new[] { Node("root") },
            new[] { Clip("c", 0, 1f, true, Curve(0, "posX", (0f, 0f), (1f, float.PositiveInfinity))) }));

    [Fact]
    public void MissingNodesField_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.FromJson("""{ "clips": [] }"""));

    [Fact]
    public void MalformedJson_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.FromJson("{ not json"));

    [Fact]
    public void UnknownChannel_Throws()
        => Assert.Throws<InvalidDataException>(() => SpriteRig.FromJson("""
        {
          "nodes": [ { "name": "a", "parent": -1, "pos": [0, 0] } ],
          "clips": [ { "name": "c", "duration": 1, "curves": [ { "node": 0, "channel": "teleport", "keys": [[0, 0]] } ] } ]
        }
        """));

    [Fact]
    public void NegativeDeltaTime_Throws()
    {
        var rig = FoldedChain();
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Update(-0.1f));
    }

    [Fact]
    public void OutOfRangeLookups_Throw()
    {
        var rig = FoldedChain();

        Assert.Throws<ArgumentException>(() => rig.GetWorldPosition("nao-existe"));
        Assert.Throws<ArgumentException>(() => rig.GetClipTime("nao-existe"));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.GetWorldPosition(99));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.SetClipTime(99, 0f));
    }

    [Fact]
    public void TryGetNodeIndex_ReportsMiss()
    {
        var rig = FoldedChain();

        Assert.True(rig.TryGetNodeIndex("leaf", out int index));
        Assert.Equal(2, index);
        Assert.False(rig.TryGetNodeIndex("nope", out int missing));
        Assert.Equal(-1, missing);
    }

    // ---------------------------------------------------------------------------------------
    // Canais e interpolação
    // ---------------------------------------------------------------------------------------

    private static SpriteRig RigWith(string channel, params (float T, float V)[] keys)
        => SpriteRig.Create(
            new[] { Node("n") },
            new[] { Clip("c", 0, 10f, true, Curve(0, channel, keys)) });

    [Fact]
    public void Active_UsesStep_SoABlinkTurnsOnAndOff()
    {
        // Um "piscar": ativo de 1,0 s a 1,2 s. Tem de ficar LIGADO durante toda a janela — com
        // interpolação suave o valor só passaria de 0,5 no meio dela e a piscada sumiria.
        var rig = RigWith("active", (0f, 0f), (1.0f, 1f), (1.2f, 0f));

        rig.Update(0f);
        Assert.False(rig.IsNodeVisible("n"));

        rig.Update(1.05f);
        Assert.True(rig.IsNodeVisible("n"));

        rig.Update(0.13f);      // t = 1,18 — ainda dentro da janela
        Assert.True(rig.IsNodeVisible("n"));

        rig.Update(0.05f);      // t = 1,23 — depois de fechar
        Assert.False(rig.IsNodeVisible("n"));
    }

    [Fact]
    public void PositionX_Smoothstep_IsHalfWayAtTheMidpoint()
    {
        var rig = RigWith("posX", (0f, 0f), (2f, 100f));
        rig.Update(0f);
        rig.Update(1f);

        Assert.Equal(50f, rig.GetWorldPosition("n").X, 3);
    }

    [Fact]
    public void PositionX_Smoothstep_EasesNearTheEnds()
    {
        var rig = RigWith("posX", (0f, 0f), (2f, 100f));
        rig.Update(0f);

        rig.Update(0.2f);                       // u = 0,1
        float early = rig.GetWorldPosition("n").X;

        rig.Update(1.6f);                       // t = 1,8 -> u = 0,9
        float late = rig.GetWorldPosition("n").X;

        Assert.True(early < 8f, $"esperado bem abaixo do linear (10), veio {early}");
        Assert.True(late > 92f, $"esperado bem acima do linear (90), veio {late}");
    }

    [Fact]
    public void PositionY_OnlyTouchesY_LeavingXFromTheRestPose()
    {
        var rig = SpriteRig.Create(
            new[] { Node("n", x: 7f, y: -3f) },
            new[] { Clip("c", 0, 4f, true, Curve(0, "posY", (0f, 0f), (4f, 40f))) });

        rig.Update(2f);                         // meio -> smoothstep(0,5) = 0,5

        var pos = rig.GetWorldPosition("n");
        Assert.Equal(7f, pos.X, 3);
        Assert.Equal(20f, pos.Y, 3);
    }

    [Fact]
    public void UnanimatedChannel_KeepsTheRestPose()
    {
        var rig = SpriteRig.Create(
            new[] { Node("n", x: 7f, y: -3f) },
            new[] { Clip("c", 0, 4f, true, Curve(0, "rotZ", (0f, 0f), (4f, 90f))) });

        rig.Update(2f);

        Assert.Equal(new Microsoft.Xna.Framework.Vector2(7f, -3f), rig.GetWorldPosition("n"));
        Assert.Equal(45f, rig.GetWorldRotationZ("n"), 3);
    }

    [Fact]
    public void Curve_ClampsBeforeTheFirstAndAfterTheLastKey()
    {
        var rig = RigWith("posX", (1f, 42f), (2f, 99f));
        rig.Update(0f);

        Assert.Equal(42f, rig.GetWorldPosition("n").X, 3);

        rig.Update(5f);
        Assert.Equal(99f, rig.GetWorldPosition("n").X, 3);
    }

    [Fact]
    public void OutOfOrderKeys_AreSortedOnLoad()
    {
        // JSON feito à mão pode vir fora de ordem; o rig ordena em vez de avaliar errado.
        var rig = RigWith("posX", (2f, 100f), (0f, 0f), (1f, 50f));
        rig.Update(0f);

        Assert.Equal(0f, rig.GetWorldPosition("n").X, 3);

        rig.Update(2f);
        Assert.Equal(100f, rig.GetWorldPosition("n").X, 3);
    }

    [Fact]
    public void MissingDuration_IsDerivedFromTheLastKey()
    {
        var rig = SpriteRig.Create(
            new[] { Node("n") },
            new[] { Clip("c", 0, 0f, true, Curve(0, "posX", (0f, 0f), (3f, 30f))) });

        Assert.Equal(3f, rig.Duration, 3);
    }

    [Fact]
    public void Loop_WrapsTheClockBackToTheStart()
    {
        var rig = RigWith("posX", (0f, 0f), (2f, 100f));
        rig.Update(0f);

        rig.Update(2.5f);
        Assert.Equal(100f, rig.GetWorldPosition("n").X, 3);

        rig.Update(2f);                         // t = 4,5
        Assert.Equal(100f, rig.GetWorldPosition("n").X, 3);

        rig.Update(5.5f);                       // t = 10 % 10 = 0
        Assert.Equal(0f, rig.GetWorldPosition("n").X, 3);
    }

    [Fact]
    public void NonLoopingClip_HoldsTheEndInsteadOfWrapping()
    {
        var rig = SpriteRig.Create(
            new[] { Node("n") },
            new[] { Clip("c", 0, 2f, loop: false, Curve(0, "posX", (0f, 0f), (2f, 100f))) });

        rig.Update(0f);
        rig.Update(5f);

        Assert.Equal(100f, rig.GetWorldPosition("n").X, 3);
        Assert.Equal(2f, rig.GetClipTime("c"), 3);
    }

    // ---------------------------------------------------------------------------------------
    // Controle de clipes e tempo
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TimeScale_ZeroPauses_TwoDoubles()
    {
        var rig = RigWith("posX", (0f, 0f), (2f, 100f));

        rig.TimeScale = 0f;
        rig.Update(5f);
        Assert.Equal(0f, rig.GetWorldPosition("n").X, 3);       // pausado

        rig.TimeScale = 2f;
        rig.Update(0.5f);                                       // 1 s de clipe
        Assert.Equal(50f, rig.GetWorldPosition("n").X, 3);
    }

    [Fact]
    public void SetClipTime_SeeksAndWrapsOnLoopedClips()
    {
        var rig = RigWith("posX", (0f, 0f), (2f, 100f));

        rig.SetClipTime("c", 1f);
        rig.Update(0f);
        Assert.Equal(50f, rig.GetWorldPosition("n").X, 3);

        rig.SetClipTime("c", 12f);                              // 12 % 10 = 2
        rig.Update(0f);
        Assert.Equal(100f, rig.GetWorldPosition("n").X, 3);

        Assert.Equal(2f, rig.GetClipTime(0), 3);
    }

    [Fact]
    public void Reset_RestoresTheRestPoseAndTheClocks()
    {
        var rig = RigWith("posX", (0f, 0f), (2f, 100f));
        rig.Update(0f);
        rig.Update(2f);
        Assert.Equal(100f, rig.GetWorldPosition("n").X, 3);

        rig.Reset();
        rig.Update(0f);

        Assert.Equal(0f, rig.GetWorldPosition("n").X, 3);
        Assert.Equal(0f, rig.GetClipTime(0), 3);
    }

    [Fact]
    public void MultipleClips_RunOnIndependentClocks()
    {
        var rig = SpriteRig.Create(
            new[] { Node("a"), Node("b", parent: 0) },
            new[]
            {
                Clip("fast", 0, 1f, true, Curve(0, "posX", (0f, 0f), (1f, 10f))),
                Clip("slow", 0, 4f, true, Curve(1, "posY", (0f, 0f), (4f, 40f))),
            });

        rig.Update(0f);
        rig.Update(1f);             // fast deu a volta (1 % 1 = 0); slow está em 1/4

        Assert.Equal(0f, rig.GetWorldPosition("a").X, 3);
        Assert.Equal(6.25f, rig.GetWorldPosition("b").Y, 3);    // smoothstep(0,25) -> 6,25
    }

    // ---------------------------------------------------------------------------------------
    // JSON (o formato que o exportador gera)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void FromJson_ReadsTheDocumentedSchema()
    {
        const string json = """
        {
          "nodes": [
            { "name": "root", "parent": -1, "pos": [0, 0], "rotZ": 0, "order": 0, "sprite": null, "optional": false },
            { "name": "leaf", "parent": 0, "pos": [10, 0], "rotZ": 90, "order": 1, "sprite": "leaf.png", "optional": false }
          ],
          "clips": [
            { "name": "idle", "root": 0, "duration": 2, "loop": true,
              "curves": [ { "node": 1, "channel": "posX", "keys": [[0, 0], [2, 20]] } ] }
          ]
        }
        """;

        var rig = SpriteRig.FromJson(json);

        Assert.Equal(2, rig.Nodes.Count);
        Assert.Equal(1, rig.Clips.Count);
        Assert.Equal("leaf.png", rig.SpritePaths[0]);
        Assert.Equal(2f, rig.Duration, 3);
    }

    [Fact]
    public void FromJson_AcceptsLegacyCeOnlyFlagAsOptional()
    {
        const string json = """
        {
          "nodes": [
            { "name": "banner", "parent": -1, "pos": [0, 0], "ceOnly": true, "sprite": "b.png" }
          ],
          "clips": []
        }
        """;

        var rig = SpriteRig.FromJson(json);

        Assert.True(rig.Nodes[0].Optional);
    }

    [Fact]
    public void Load_ReadsFromAStream()
    {
        const string json = """
        { "nodes": [ { "name": "root", "parent": -1, "pos": [0, 0] } ], "clips": [] }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var rig = SpriteRig.Load(stream);

        Assert.Single(rig.Nodes);
    }

    [Fact]
    public void AssignTextures_UsesTheDeclaredPathsInOrder()
    {
        var rig = SpriteRig.Create(
            new[] { Node("a", sprite: "one.png"), Node("b", sprite: "two.png"), Node("c", sprite: "one.png") },
            Array.Empty<SpriteRigClip>());

        var asked = new System.Collections.Generic.List<string>();
        rig.AssignTextures(path => { asked.Add(path); return null; });

        Assert.Equal(new[] { "one.png", "two.png" }, asked);
        Assert.Equal(2, rig.Images.Length);
    }

    // ---------------------------------------------------------------------------------------
    // Ordem de desenho e acesso por nó (para renderizadores próprios)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void DrawOrder_SortsByOrderThenByHierarchy()
    {
        var rig = SpriteRig.Create(
            new[]
            {
                Node("back", order: 5),
                Node("front", order: 0),
                Node("mid", order: 2),
                Node("midChild", parent: 2, order: 2),   // mesmo order: o filho desenha depois
            },
            Array.Empty<SpriteRigClip>());

        Assert.Equal(new[] { 1, 2, 3, 0 }, rig.DrawOrder);
    }

    [Fact]
    public void GetNodeImage_ReturnsNullForPivotsAndUnassignedSprites()
    {
        var rig = SpriteRig.Create(
            new[] { Node("pivot"), Node("leaf", parent: 0, sprite: "leaf.png") },
            Array.Empty<SpriteRigClip>());

        Assert.Null(rig.GetNodeImage(0));       // pivô: não tem sprite
        Assert.Null(rig.GetNodeImage(1));       // imagem ainda não registrada
    }

    [Fact]
    public void AssignImages_IsTheAtlasPath_AndSkipsMissingSprites()
    {
        var rig = SpriteRig.Create(
            new[] { Node("a", sprite: "horseshoe"), Node("b", sprite: "pig"), Node("c", sprite: "sumiu") },
            Array.Empty<SpriteRigClip>());

        var asked = new System.Collections.Generic.List<string>();
        rig.AssignImages(key =>
        {
            asked.Add(key);

            // O rig não sabe o que é um atlas: o jogo devolve textura + recorte. Aqui basta provar
            // que a chave chega e que um sprite ausente (null) é aceito sem erro.
            if (key == "sumiu")
                return null;

            return new SpriteRigImage(null!, new Microsoft.Xna.Framework.Rectangle(0, 0, 10, 20));
        });

        Assert.Equal(new[] { "horseshoe", "pig", "sumiu" }, asked);
        Assert.Equal(3, rig.Images.Length);
        Assert.Equal(10, rig.Images[0]!.Value.Width);
        Assert.Equal(20, rig.Images[0]!.Value.Height);

        // Imagem com textura nula (ou descartada) não desenha e não derruba a consulta.
        Assert.Null(rig.GetNodeImage(0));
        Assert.Null(rig.GetNodeImage(2));
    }

    [Fact]
    public void SpriteRigImage_Whole_UsesTheFullTexture_AndRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => SpriteRigImage.Whole(null!));
    }

    [Fact]
    public void DrawOrderAndImageLookup_RejectInvalidNodeIndex()
    {
        var rig = FoldedChain();

        Assert.Throws<ArgumentOutOfRangeException>(() => rig.GetNodeImage(99));
    }
}
