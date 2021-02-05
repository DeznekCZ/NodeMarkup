﻿using CitiesHarmony.API;
using ColossalFramework;
using ColossalFramework.Packaging;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using HarmonyLib;
using ModsCommon;
using ModsCommon.Utilities;
using NodeMarkup.Manager;
using NodeMarkup.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace NodeMarkup
{
    public class Patcher : Patcher<Mod>
    {
        protected override bool PatchProcess()
        {
            var success = true;

            success &= PatchNetNodeRenderInstance();
            success &= PatchNetSegmentRenderInstance();

            success &= PatchNetManagerReleaseNodeImplementation();
            success &= PatchNetManagerReleaseSegmentImplementation();

            success &= PatchNetManagerUpdateNode();
            success &= PatchNetManagerUpdateSegment();
            success &= PatchNetSegmentUpdateLanes();

            success &= PatchNetManagerSimulationStepImpl();

            success &= PatchBuildingDecorationLoadPaths();

            success &= PatchLoadAssetPanelOnLoad();

            if (Settings.RailUnderMarking)
                success &= PatchNetInfoNodeInitNodeInfo();

            if (Settings.LoadMarkingAssets)
            {
                success &= PatchLoadingManagerLoadCustomContent();
                success &= PatchLoadingScreenModLoadImpl();
            }

            if (!ItemsExtension.InGame)
                Mod.Instance.LoadedError();

            return success;
        }

        private bool PatchNetNodeRenderInstance()
        {
            static MethodInfo OriginalGetter(Type type, string method) => AccessTools.Method(type, method, new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(NetInfo), typeof(int), typeof(NetNode.Flags), typeof(uint).MakeByRefType(), typeof(RenderManager.Instance).MakeByRefType() });
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetNodeRenderInstancePostfix));

            return AddPostfix(postfix, typeof(NetNode), nameof(NetNode.RenderInstance), OriginalGetter);
        }

        private bool PatchNetSegmentRenderInstance()
        {
            static MethodInfo OriginalGetter(Type type, string method) => AccessTools.Method(type, method, new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int), typeof(NetInfo), typeof(RenderManager.Instance).MakeByRefType() });
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetSegmentRenderInstancePostfix));

            return AddPostfix(postfix, typeof(NetSegment), nameof(NetSegment.RenderInstance), OriginalGetter);
        }

        private bool PatchNetManagerUpdateNode()
        {
            static MethodInfo OriginalGetter(Type type, string method) => AccessTools.Method(type, method, new Type[] { typeof(ushort), typeof(ushort), typeof(int) });
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetManagerUpdateNodePostfix));

            return AddPostfix(postfix, typeof(NetManager), nameof(NetManager.UpdateNode), OriginalGetter);
        }

        private bool PatchNetManagerUpdateSegment()
        {
            static MethodInfo OriginalGetter(Type type, string method) => AccessTools.Method(type, method, new Type[] { typeof(ushort), typeof(ushort), typeof(int) });
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetManagerUpdateSegmentPostfix));

            return AddPostfix(postfix, typeof(NetManager), nameof(NetManager.UpdateSegment), OriginalGetter);
        }

        private bool PatchNetManagerReleaseNodeImplementation()
        {
            static MethodInfo OriginalGetter(Type type, string method) => AccessTools.Method(type, method, new Type[] { typeof(ushort), typeof(NetNode).MakeByRefType() });
            var prefix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetManagerReleaseNodeImplementationPrefix));

            return AddPrefix(prefix, typeof(NetManager), "ReleaseNodeImplementation", OriginalGetter);
        }
        private bool PatchNetManagerReleaseSegmentImplementation()
        {
            static MethodInfo OriginalGetter(Type type, string method) => AccessTools.Method(type, method, new Type[] { typeof(ushort), typeof(NetSegment).MakeByRefType(), typeof(bool) });
            var prefix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetManagerReleaseSegmentImplementationPrefix));

            return AddPrefix(prefix, typeof(NetManager), "ReleaseSegmentImplementation", OriginalGetter);
        }

        private bool PatchNetSegmentUpdateLanes()
        {
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetSegmentUpdateLanesPostfix));

            return AddPostfix(postfix, typeof(NetSegment), nameof(NetSegment.UpdateLanes));
        }

        private bool PatchNetManagerSimulationStepImpl()
        {
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetManagerSimulationStepImplPostfix));

            return AddPostfix(postfix, typeof(NetManager), "SimulationStepImpl");
        }
        private bool PatchBuildingDecorationLoadPaths()
        {
            var transpiler = AccessTools.Method(typeof(Patcher), nameof(Patcher.BuildingDecorationLoadPathsTranspiler));

            return AddTranspiler(transpiler, typeof(BuildingDecoration), nameof(BuildingDecoration.LoadPaths));
        }
        private static IEnumerable<CodeInstruction> BuildingDecorationLoadPathsTranspiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            var segmentBufferField = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempSegmentBuffer));
            var nodeBufferField = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempNodeBuffer));
            var clearMethod = AccessTools.DeclaredMethod(nodeBufferField.FieldType, nameof(FastList<ushort>.Clear));

            var matchCount = 0;
            var inserted = false;
            var enumerator = instructions.GetEnumerator();
            var prevInstruction = (CodeInstruction)null;
            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;

                if (prevInstruction != null && prevInstruction.opcode == OpCodes.Ldfld && prevInstruction.operand == nodeBufferField && instruction.opcode == OpCodes.Callvirt && instruction.operand == clearMethod)
                    matchCount += 1;

                if (!inserted && matchCount == 2)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, segmentBufferField);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, nodeBufferField);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(MarkupManager), nameof(MarkupManager.PlaceIntersection)));
                    inserted = true;
                }

                if (prevInstruction != null)
                    yield return prevInstruction;

                prevInstruction = instruction;
            }

            if (prevInstruction != null)
                yield return prevInstruction;
        }
        private bool PatchLoadAssetPanelOnLoad()
        {
            var postfix = AccessTools.Method(typeof(AssetDataExtension), nameof(AssetDataExtension.LoadAssetPanelOnLoadPostfix));

            return AddPostfix(postfix, typeof(LoadAssetPanel), nameof(LoadAssetPanel.OnLoad));
        }
        private bool PatchNetInfoNodeInitNodeInfo()
        {
            var postfix = AccessTools.Method(typeof(MarkupManager), nameof(MarkupManager.NetInfoNodeInitNodeInfoPostfix));

            return AddPostfix(postfix, typeof(NetInfo), "InitNodeInfo");
        }
        private bool PatchLoadingManagerLoadCustomContent()
        {
            var nestedType = typeof(LoadingManager).GetNestedTypes(AccessTools.all).FirstOrDefault(t => t.FullName.Contains("LoadCustomContent"));
            var transpiler = AccessTools.Method(typeof(Patcher), nameof(Patcher.LoadingManagerLoadCustomContentTranspiler));

            return AddTranspiler(transpiler, nestedType, "MoveNext");
        }

        private static IEnumerable<CodeInstruction> LoadingManagerLoadCustomContentTranspiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            var type = typeof(LoadingManager).GetNestedTypes(AccessTools.all).FirstOrDefault(t => t.FullName.Contains("LoadCustomContent"));
            var field = AccessTools.Field(type, "<metaData>__4");
            var additional = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, 19),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, field),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(CustomAssetMetaData), nameof(CustomAssetMetaData.assetRef))),
            };

            return LoadingTranspiler(instructions, OpCodes.Ldloc_S, 26, additional);
        }
        private bool PatchLoadingScreenModLoadImpl()
        {
            try
            {
                var type = AccessTools.TypeByName("LoadingScreenMod.AssetLoader") ?? AccessTools.TypeByName("LoadingScreenModTest.AssetLoader");
                var transpiler = AccessTools.Method(typeof(Patcher), nameof(Patcher.LoadingScreenModLoadImplTranspiler));
                return AddTranspiler(transpiler, type, "LoadImpl");
            }
            catch (Exception error)
            {
                Mod.Logger.Error($"LSM not founded", error);
                return true;
            }
        }
        private static IEnumerable<CodeInstruction> LoadingScreenModLoadImplTranspiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            var additional = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Ldarg_1),
            };

            return LoadingTranspiler(instructions, OpCodes.Stloc_S, 12, additional);
        }
        private static IEnumerable<CodeInstruction> LoadingTranspiler(IEnumerable<CodeInstruction> instructions, OpCode startOc, int startOp, CodeInstruction[] additional)
        {
            var enumerator = instructions.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;
                yield return instruction;

                if (instruction.opcode == startOc && instruction.operand is LocalBuilder local && local.LocalIndex == startOp)
                    break;
            }

            var elseLabel = (Label)default;
            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;
                yield return instruction;

                if (instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S)
                {
                    if (instruction.operand is Label label)
                        elseLabel = label;

                    break;
                }
            }

            if (elseLabel == default)
                throw new Exception("else label not founded");

            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;
                yield return instruction;

                if (instruction.labels.Contains(elseLabel))
                {
                    foreach (var additionalInst in additional)
                        yield return additionalInst;

                    yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Loader), nameof(Loader.LoadTemplateAsset)));
                    break;
                }
            }

            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}
