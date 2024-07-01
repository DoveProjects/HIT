using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using IConfig;

namespace HIT;

enum CustomTransform
{
    ShieldDefault,
    ShieldOnBackpack,
    ShieldOnHunterPack,
    HammerLeft,
    HammerRight
}

public class ToolRenderer : IRenderer
{
    private readonly ICoreClientAPI _api;

    private readonly float[] _modelMat = Mat4f.Create();

    private readonly MultiTextureMeshRef[] _playerTools = new MultiTextureMeshRef[HITModSystem.TotalSlots];
    private readonly string[] _slotCodes = new string[HITModSystem.TotalSlots];
    private readonly int[] _textures = new int[HITModSystem.TotalSlots];

    private readonly IPlayer _player;
    private readonly IRenderAPI _rpi;

    public double RenderOrder => 0.41;

    public int RenderRange => 200;

    private CustomTransform _shieldTransform = CustomTransform.ShieldDefault;
    private float _backToolsOffset;

    //these transforms are in relation to the player and hold the 4 slots on the body
    private static ModelTransform[] ToolTransforms = new ModelTransform[] {
        new() //slot 0 left forearm, pointed down, can hold tier 1 tools
        {
            Translation = new Vec3f(-1.3f, -0.52f, -0.695f), // z = -1f
            Rotation = new Vec3f(180, 180, 90),
            Scale = 0.65f
        },
        new() //slot 1 right forearm pointed down, can hold tier 1 tools
        {
            Translation = new Vec3f(-1.25f, -0.46f, -0.875f),
            Rotation = new Vec3f(180, 180, 90),
            Scale = 0.65f
        },
        new() //slot 2 diagonal across back, pointed up, can hold tier 4 tools
        {
            Translation = new Vec3f(-0.05f, -0.69f, -0.54f), //the one at 10 o clock
            Rotation = new Vec3f(45, 0, -90),
            Scale = 0.85f
        },
        new() //slot 3 diagonal across back, pointed up, can hold tier 4 tools
        {
            Translation = new Vec3f(-0.975f, -0.69f, -0.54f), //the one at 2 o clock previously -0.5
            Rotation = new Vec3f(-45, 180, -90),
            Scale = 0.85f
        },
    };

    //these are alternative transforms for special cases. You can also modify the original transforms for small things as used later for shield offset, but it's clunky.
    private static Dictionary<CustomTransform, ModelTransform> CustomTransforms = new()
    {
        [CustomTransform.ShieldDefault] = new()
        { //shield directly on back
            Translation = new Vec3f(-0.2f, -0.22f, -0.96f), //x is front and back z is left and right
            Rotation = new Vec3f(0, 90, 0), //x is roll y is yaw z is pitch
            Scale = 0.9f
        },
        [CustomTransform.ShieldOnBackpack] = new()
        { //shield on backpack
            Translation = new Vec3f(0.3f, -0.41f, 0.06f),
            Rotation = new Vec3f(45, 90, 0),
            Origin = new Vec3f(0, 0, 0),
            Scale = 0.8f
        },
        [CustomTransform.ShieldOnHunterPack] = new()
        { //shield on hunter backpack
            Translation = new Vec3f(0.2f, -0.3f, 0.066f),
            Rotation = new Vec3f(45, 90, 0),
            Origin = new Vec3f(0, 0, 0),
            Scale = 0.8f
        },
        [CustomTransform.HammerLeft] = new()  //hammer on back, diagonal from left to right
        {
            Translation = new Vec3f(-0.41f, -0.68f, -0.50f), //the one at 10 o clock
            Rotation = new Vec3f(45, 0, -90),
            Scale = 0.95f
        },
        [CustomTransform.HammerRight] = new() //hammer on back, diagonal from right to left
        {
            Translation = new Vec3f(-0.43f, -0.68f, -0.50f), //the one at 2 o clock previously -0.5
            Rotation = new Vec3f(-45, 180, -90),
            Scale = 0.95f
        }
    };

    //setting up variables, api, and renderer
    public ToolRenderer(ICoreClientAPI api, IPlayer player)
    {
        _api = api;
        _rpi = _api.Render;
        _player = player;

        _api.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        _api.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear);
        _api.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar);
    }

    //checks for special cases due to saw/hammer having a weird offset and the shield needing its own specific slot for transforms
    private bool TryGetCustomTransform(int slotId, out ModelTransform transform)
    {
        var code = _slotCodes[slotId];

        if (code != null)
        {
            if (code.Contains("hammer") || code.Contains("saw"))
            {
                transform = slotId == 2
                    ? CustomTransforms[CustomTransform.HammerLeft]
                    : CustomTransforms[CustomTransform.HammerRight];
                return true;
            }

            if (code.Contains("shield") && slotId == HITModSystem.ShieldSlotId)
            {
                transform = CustomTransforms[_shieldTransform];
                return true;
            }
        }

        transform = null;
        return false;
    }

    public void UpdateRenderedTools(UpdatePlayerTools message)
    {
        //clear the arrays to avoid the previous models overriding new ones / wiping now-empty slots
        Array.Clear(_playerTools, 0, _playerTools.Length);
        Array.Clear(_slotCodes, 0, _slotCodes.Length);

        for (var i = 0; i < _playerTools.Length; i++) //map the _playerTools array to the _bodyArray data sent over from PlayerToolWatcher
        {
            if (!message.RenderedTools.TryGetValue(i, out var slotData) || slotData == null) continue;

            var item = _api.World.GetItem(new AssetLocation(slotData.Code));
            if (item == null) continue;

            _slotCodes[i] = slotData.Code;
            var stack = new ItemStack(slotData.StackData);

            //Here we perform our checks against the options set in the client config

            LoadToolMultiMesh(stack, item, i);
        }
        _backToolsOffset = 0;

        switch (message.BackPackType) //the switch sorts it based on the backpack type into the transform so it can be picked up later
        {
            case BackPackType.Leather:
                _shieldTransform = CustomTransform.ShieldOnBackpack;
                break;
            case BackPackType.Hunter:
                _shieldTransform = CustomTransform.ShieldOnHunterPack;
                break;
            case BackPackType.None:
                _shieldTransform = CustomTransform.ShieldDefault;

                if (_playerTools[HITModSystem.ShieldSlotId] != null)
                {
                    _backToolsOffset = 0.075f;
                }

                break;
        }
    }

    private void LoadToolMultiMesh(ItemStack itemStack, Item item, int slotIndex)
    {
        //cacheing system. Search "cache" to see where it's used.
        var cache = ObjectCacheUtil.GetOrCreate(
            _api, "EquipToolRenderCache", () => new Dictionary<string, (int texture, MultiTextureMeshRef mesh)>());
        
        var cacheKey = item.Code.ToString();
        //this mess is just about setting up the itemStack to be put into the cache
        if (itemStack.Attributes.Count > 0)
        {
            var dict = itemStack.Attributes.ToImmutableSortedDictionary();
            var builder = new StringBuilder();
            foreach (var (_, val) in dict)
            {
                var str = val.ToString();
                if (string.IsNullOrEmpty(str)) continue;

                builder.Append(str);
            }
            cacheKey += builder.ToString();
        }

        //if cache doesn't get a value, it returns false. This triggers when that happens to set up a mesh from the itemStack
        if (!cache.TryGetValue(cacheKey, out var info))
        {
            var texSource = _api.Tesselator.GetTextureSource(item);

            TesselationMetaData meta = new()
            {
                QuantityElements = item.Shape.QuantityElements,
                SelectiveElements = item.Shape.SelectiveElements,
                TexSource = texSource,
                TypeForLogging = item.Code.ToString()
            };

            var loc = item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            var shape = Shape.TryGet(_api, loc).Clone();

            shape.ResolveReferences(_api.Logger, item.Code.ToString());
            shape.ResolveAndFindJoints(_api.Logger, item.Code.ToString());

            _api.Tesselator.TesselateShape(meta, shape, out var itemMesh);

            if (item is ItemShield shield)
            {
                var mesh = shield.GenMesh(itemStack, _api.ItemTextureAtlas);
                if (mesh != null)
                {
                    info = new(_api.ItemTextureAtlas.AtlasTextures[0].TextureId, _rpi.UploadMultiTextureMesh(mesh));
                    cache[cacheKey] = info;
                }

            }
            else
            {
                info = new(_api.ItemTextureAtlas.AtlasTextures[0].TextureId, _rpi.UploadMultiTextureMesh(itemMesh));
                cache[cacheKey] = info;
            }

        }
        if (info.mesh == null) return;

        _textures[slotIndex] = info.texture;
        _playerTools[slotIndex] = info.mesh; //dev note: set the textures / meshes at the end instead of inside the switch case if you have one
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (_player == _api.World.Player && _api.World.Player.CameraMode == EnumCameraMode.FirstPerson) return; //if it's in first person mode skip the rendering as a whole
        if (_player.Entity.Properties.Client.Renderer is not EntityShapeRenderer rend) return; //checking for modded
        if (_player.Entity.AnimManager.Animator is not ClientAnimator animator) return; //checking for modded
        //setting up the renderer
        bool isShadowPass = stage != EnumRenderStage.Opaque;
        var skippedLeft = false;
        var skippedRight = false;
        var prog = isShadowPass
            ? null
            : _rpi.PreparedStandardShader((int)_player.Entity.Pos.X, (int)_player.Entity.Pos.Y, (int)_player.Entity.Pos.Z);
        //looping through the tool inventory
        for (int j = 0; j < _playerTools.Length; j++)
        {
            if (_playerTools[j] == null) continue; //if empty continue

            if (_slotCodes[j] == _player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code.ToString() && !skippedRight) //if it's in the players right hand skip render
            {
                skippedRight = true;
                continue;
            }

            if (_slotCodes[j] == _player.Entity.LeftHandItemSlot?.Itemstack?.Collectible?.Code.ToString() && !skippedLeft) //if its in the player's left hand skip render
            { 
                skippedLeft = true;
                continue;
            }

            RenderTool(j, prog, rend, animator); //render tool
        }

        prog?.Stop();
    }

    private void RenderTool(int slotId, IStandardShaderProgram prog, EntityShapeRenderer rend, ClientAnimator animator)
    {
        if (_playerTools[slotId] == null) return; //if the mesh has no reference set skip

        var toolTextureId = _textures[slotId]; //set the texture
        if (!TryGetCustomTransform(slotId, out var toolTransform) && slotId < ToolTransforms.Length) //if it needs a custom transform
        {
            toolTransform = ToolTransforms[slotId];
        }

        if (toolTransform == null) return; //if it has no transforms skip

        var attachmentPointName = slotId switch //character only has 4 attachment points, hands back and head, so it's using that + transforms for positioning
        {
            0 => "LeftHand",
            1 => "RightHand",
            _ => "Back"
        };

        animator.AttachmentPointByCode.TryGetValue(attachmentPointName, out var apap);

        if (apap == null) return;
        
        //small value for if the back sheaths need to be reset to a different default position (only useful if previous render pass had a shield equipped and this didnt)
        var offsetX = slotId is 2 or 3 ? _backToolsOffset : 0f;

        //this is just retrieving the transform info for rendering
        var ap = apap.AttachPoint;
        Mat4f.Copy(_modelMat, rend.ModelMat);
        Mat4f.Mul(_modelMat, _modelMat, apap.CachedPose.AnimModelMatrix);

        Mat4f.Translate(_modelMat, _modelMat, toolTransform.Origin.X, toolTransform.Origin.Y, toolTransform.Origin.Z);
        Mat4f.Scale(_modelMat, _modelMat, toolTransform.ScaleXYZ.X, toolTransform.ScaleXYZ.Y, toolTransform.ScaleXYZ.Z);
        Mat4f.Translate(
            _modelMat, _modelMat, (float)ap.PosX / 16f + toolTransform.Translation.X + offsetX,
            (float)ap.PosY / 16f + toolTransform.Translation.Y, (float)ap.PosZ / 16f + toolTransform.Translation.Z);
        Mat4f.RotateX(_modelMat, _modelMat, (float)(ap.RotationX + toolTransform.Rotation.X) * GameMath.DEG2RAD);
        Mat4f.RotateY(_modelMat, _modelMat, (float)(ap.RotationY + toolTransform.Rotation.Y) * GameMath.DEG2RAD);
        Mat4f.RotateZ(_modelMat, _modelMat, (float)(ap.RotationZ + toolTransform.Rotation.Z) * GameMath.DEG2RAD);
        Mat4f.Translate(_modelMat, _modelMat, -toolTransform.Origin.X, -toolTransform.Origin.Y, -toolTransform.Origin.Z);

        var currentShader = _rpi.CurrentActiveShader;
        //setting up shaders
        if (prog != null)
        {
            prog.UniformMatrix("modelMatrix", _modelMat);
            prog.UniformMatrix("viewMatrix", _rpi.CameraMatrixOriginf);
            prog.DontWarpVertices = 0;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            _api.Render.RenderMultiTextureMesh(_playerTools[slotId], "tex");
        }
        else if (currentShader != null)
        {
            Mat4f.Mul(_modelMat, _rpi.CurrentShadowProjectionMatrix, _modelMat);
            currentShader.UniformMatrix("mvpMatrix", _modelMat);
            currentShader.Uniform("origin", rend.OriginPos);
            currentShader.BindTexture2D("tex2d", toolTextureId, 0);

            _api.Render.RenderMultiTextureMesh(_playerTools[slotId], "tex2d");

        }
    }
    //to prevent memory leaks
    public void Dispose()
    {
        _api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        _api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowNear);
        _api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowFar);
    }
}