﻿using ColossalFramework.Math;
using ModsCommon.UI;
using ModsCommon.Utilities;
using NodeMarkup.Manager;
using NodeMarkup.UI;
using NodeMarkup.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ToolBase;
using ColossalFramework.UI;
using ColossalFramework;

namespace NodeMarkup.Tools
{
    public class SelectToolMode : BaseToolMode
    {
        public override ToolModeType Type => ToolModeType.Select;
        public override bool ShowPanel => false;

        private NodeSelection HoverNode { get; set; } = null;
        private bool IsHoverNode => HoverNode != null;

        private SegmentSelection HoverSegment { get; set; } = null;
        private bool IsHoverSegment => HoverSegment != null;

        protected override void Reset(BaseToolMode prevMode)
        {
            HoverNode = null;
            HoverSegment = null;
        }

        public override void OnToolUpdate()
        {
            NodeSelection nodeSelection = null;
            SegmentSelection segmentSelection = null;

            if (NodeMarkupTool.MouseRayValid)
            {
                if (IsHoverNode && HoverNode.Contains(NodeMarkupTool.MouseWorldPosition))
                    nodeSelection = HoverNode;
                else if (IsHoverSegment && HoverSegment.Contains(NodeMarkupTool.MouseWorldPosition))
                    segmentSelection = HoverSegment;
                else
                    RayCast(out nodeSelection, out segmentSelection);
            }

            HoverNode = nodeSelection;
            HoverSegment = segmentSelection;
        }

        private void RayCast(out NodeSelection nodeSelection, out SegmentSelection segmentSelection)
        {
            var hitPos = NodeMarkupTool.MouseWorldPosition;
            var gridMinX = Max(hitPos.x);
            var gridMinZ = Max(hitPos.z);
            var gridMaxX = Min(hitPos.x);
            var gridMaxZ = Min(hitPos.z);
            var segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            var checkedNodes = new HashSet<ushort>();

            for (int i = gridMinZ; i <= gridMaxZ; i++)
            {
                for (int j = gridMinX; j <= gridMaxX; j++)
                {
                    var segmentId = NetManager.instance.m_segmentGrid[i * 270 + j];
                    int count = 0;

                    while (segmentId != 0u && count < 36864)
                    {
                        if (CheckSegment(segmentId))
                        {
                            var segment = segmentId.GetSegment();

                            if (!checkedNodes.Contains(segment.m_startNode))
                            {
                                if (RayCastNode(segment.m_startNode, hitPos, out nodeSelection))
                                {
                                    segmentSelection = null;
                                    return;
                                }
                                else
                                    checkedNodes.Add(segment.m_startNode);
                            }
                            if (!checkedNodes.Contains(segment.m_endNode))
                            {
                                if (RayCastNode(segment.m_endNode, hitPos, out nodeSelection))
                                {
                                    segmentSelection = null;
                                    return;
                                }
                                else
                                    checkedNodes.Add(segment.m_endNode);
                            }
                            if (RayCastSegments(segmentId, hitPos, out segmentSelection))
                            {
                                nodeSelection = null;
                                return;
                            }
                        }

                        segmentId = segmentBuffer[segmentId].m_nextGridSegment;
                    }
                }
            }

            nodeSelection = null;
            segmentSelection = null;

            static bool RayCastNode(ushort nodeId, Vector3 hitPos, out NodeSelection selection)
            {
                selection = new NodeSelection(nodeId);
                return selection.Contains(hitPos);
            }
            static bool RayCastSegments(ushort segmentId, Vector3 hitPos, out SegmentSelection selection)
            {
                selection = new SegmentSelection(segmentId);
                return selection.Contains(hitPos);
            }
            static int Max(float value) => Mathf.Max((int)((value - 16f) / 64f + 135f) - 1, 0);
            static int Min(float value) => Mathf.Min((int)((value + 16f) / 64f + 135f) + 1, 269);
        }
        private bool CheckSegment(ushort segmentId)
        {
            var segment = segmentId.GetSegment();
            var connect = segment.Info.GetConnectionClass();

            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            if ((connect.m_layer & ItemClass.Layer.Default) == 0)
                return false;

            if (connect.m_service != ItemClass.Service.Road && (connect.m_service != ItemClass.Service.PublicTransport || connect.m_subService != ItemClass.SubService.PublicTransportPlane))
                return false;

            return true;
        }


        public override string GetToolInfo() => IsHoverNode ? string.Format(Localize.Tool_InfoHoverNode, HoverNode.Id) : (IsHoverSegment ? string.Format(Localize.Tool_InfoHoverSegment, HoverSegment.Id) : Localize.Tool_SelectInfo);

        public override void OnMouseUp(Event e) => OnPrimaryMouseClicked(e);
        public override void OnPrimaryMouseClicked(Event e)
        {
            var markup = default(Markup);
            if (IsHoverNode)
                markup = MarkupManager.NodeManager.Get(HoverNode.Id);
            else if (IsHoverSegment)
                markup = MarkupManager.SegmentManager.Get(HoverSegment.Id);
            else
                return;

            Mod.Logger.Debug($"Select marking {markup}");
            Tool.SetMarkup(markup);

            if (markup.NeedSetOrder)
            {
                var messageBox = MessageBoxBase.ShowModal<YesNoMessageBox>();
                messageBox.CaptionText = Localize.Tool_RoadsWasChangedCaption;
                messageBox.MessageText = Localize.Tool_RoadsWasChangedMessage;
                messageBox.OnButton1Click = OnYes;
                messageBox.OnButton2Click = OnNo;
            }
            else
                OnNo();

            bool OnYes()
            {
                BaseOrderToolMode.IntersectionTemplate = markup.Backup;
                Tool.SetMode(ToolModeType.EditEntersOrder);
                markup.NeedSetOrder = false;
                return true;
            }
            bool OnNo()
            {
                Tool.SetDefaultMode();
                markup.NeedSetOrder = false;
                return true;
            }
        }
        public override void OnSecondaryMouseClicked() => Tool.Disable();
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (IsHoverNode)
                HoverNode.Render(new OverlayData(cameraInfo) { Color = Colors.Orange });
            else if (IsHoverSegment)
                HoverSegment.Render(new OverlayData(cameraInfo) { Color = Colors.Purple });
        }
    }

    public abstract class Selection : IOverlay
    {
        protected static float OverlayWidth => 2f;

        public ushort Id { get; }
        protected Data[] Datas { get; }
        protected Vector3 Center { get; set; }
        protected abstract Vector3 Position { get; }
        protected abstract float HalfWidth { get; }
        protected IEnumerable<ITrajectory> BorderLines
        {
            get
            {
                for (var i = 0; i < Datas.Length; i += 1)
                {
                    yield return new StraightTrajectory(Datas[i].leftPos, Datas[i].rightPos);

                    var j = (i + 1) % Datas.Length;
                    if (Datas.Length != 1)
                        yield return new BezierTrajectory(GetBezier(Datas[i].leftPos, Datas[i].leftDir, Datas[j].rightPos, Datas[j].rightDir));
                    else
                        yield return new BezierTrajectory(GetEndBezier(Datas[i].leftPos, Datas[i].leftDir, Datas[j].rightPos, Datas[j].rightDir));
                }
            }
        }
        public Selection(ushort id)
        {
            Id = id;
            Datas = Calculate().OrderBy(s => s.angle).ToArray();
            CalculateCenter();
        }
        protected abstract IEnumerable<Data> Calculate();
        public abstract void Render(OverlayData overlayData);
        private void CalculateCenter()
        {
            if (Datas.Length == 1)
                Center = Position + Datas[0].dir;
            else
            {
                Vector3 center = new();
                for (var i = 0; i < Datas.Length; i += 1)
                {
                    var j = (i + 1) % Datas.Length;

                    var bezier = GetBezier(Datas[i].Position, Datas[i].dir, Datas[j].Position, Datas[j].dir);
                    center += bezier.Position(0.5f);
                }
                Center = center / Datas.Length;
            }
        }
        public virtual bool Contains(Vector3 hitPos)
        {
            var line = new StraightTrajectory(hitPos, Center);
            var contains = !BorderLines.Any(b => MarkupIntersect.CalculateSingle(line, b).IsIntersect);
            return contains;
        }

        protected void Render(OverlayData overlayData, Data data1, Data data2, bool isEndBezier = false)
        {
            var count = Math.Max(Mathf.CeilToInt(2 * data1.halfWidth / (OverlayWidth * 0.75f)), Mathf.CeilToInt(2 * data2.halfWidth / (OverlayWidth * 0.75f)));

            var step1 = data1.GetStep(count);
            var step2 = data2.GetStep(count);

            var ratio1 = data1.Ratio;
            var ratio2 = data2.Ratio;

            var cornerDir1 = data1.CornerDir;
            var cornerDir2 = data2.CornerDir;

            for (var l = 0; l < count; l += 1)
            {
                var pos1 = data1.leftPos + cornerDir1 * (OverlayWidth / 2 + l * step1) * ratio1;
                var pos2 = data2.rightPos - cornerDir2 * (OverlayWidth / 2 + l * step2) * ratio2;
                Bezier3 bezier;
                if (!isEndBezier)
                    bezier = GetBezier(pos1, data1.dir, pos2, data2.dir);
                else
                {
                    var ratio = Mathf.Abs(count - 2 * l) / (float)count;
                    bezier = GetEndBezier(pos1, data1.dir, pos2, data2.dir, OverlayWidth / 2, ratio);
                }
                NodeMarkupTool.RenderBezier(bezier, overlayData);
            }
        }
        private Bezier3 GetBezier(Vector3 leftPos, Vector3 leftDir, Vector3 rightPos, Vector3 rightDir)
        {
            var bezier = new Bezier3()
            {
                a = leftPos,
                d = rightPos,
            };

            NetSegment.CalculateMiddlePoints(bezier.a, leftDir, bezier.d, rightDir, true, true, out bezier.b, out bezier.c);
            return bezier;
        }
        private Bezier3 GetEndBezier(Vector3 leftPos, Vector3 leftDir, Vector3 rightPos, Vector3 rightDir, float halfWidth = 0f, float ratio = 1f)
        {
            var length = Mathf.Min((leftPos - rightPos).XZ().magnitude, 8f - halfWidth) * ratio / 0.75f;
            var bezier = new Bezier3()
            {
                a = leftPos,
                b = leftPos + leftDir * length,
                c = rightPos + rightDir * length,
                d = rightPos,
            };
            return bezier;
        }
        protected void Render(OverlayData overlayData, Data data)
        {
            var cornerDir = data.CornerDir * data.Ratio * (OverlayWidth / 2);
            var line = new StraightTrajectory(data.leftPos + cornerDir, data.rightPos - cornerDir);
            line.Render(overlayData);
        }

        protected struct Data
        {
            public float angle;
            public Vector3 rightPos;
            public Vector3 leftPos;
            public Vector3 rightDir;
            public Vector3 leftDir;
            public Vector3 dir;
            public float halfWidth;

            public float Ratio => (rightPos - leftPos).XZ().magnitude / (2 * halfWidth);
            public Vector3 CornerDir => (rightPos - leftPos).normalized;
            public StraightTrajectory Line => new StraightTrajectory(leftPos, rightPos);
            public Vector3 Position => (rightPos + leftPos) / 2;

            public float GetStep(int count) => (2 * halfWidth - OverlayWidth) / (count - 1);
        }
    }
    public class NodeSelection : Selection
    {
        protected override Vector3 Position => Id.GetNode().m_position;
        protected override float HalfWidth => Id.GetNode().Info.m_halfWidth;
        public NodeSelection(ushort id) : base(id) { }

        protected override IEnumerable<Data> Calculate()
        {
            var node = Id.GetNode();

            foreach (var segmentId in node.SegmentsId())
            {
                var segment = segmentId.GetSegment();
                var isStart = segment.m_startNode == Id;
                var data = new Data()
                {
                    halfWidth = segment.Info.m_halfWidth.RoundToNearest(0.1f),
                    dir = (isStart ? -segment.m_startDirection : -segment.m_endDirection).normalized,
                };
                data.angle = (-data.dir).AbsoluteAngle();

                segment.CalculateCorner(segmentId, true, isStart, true, out data.leftPos, out data.leftDir, out _);
                segment.CalculateCorner(segmentId, true, isStart, false, out data.rightPos, out data.rightDir, out _);

                data.leftDir = (-data.leftDir).normalized;
                data.rightDir = (-data.rightDir).normalized;

                yield return data;
            }
        }
        public override bool Contains(Vector3 hitPos)
        {
            var node = Id.GetNode();
            return (node.m_flags & NetNode.Flags.Middle) == 0 && base.Contains(hitPos);
        }
        public override void Render(OverlayData overlayData)
        {
            overlayData.Width = OverlayWidth;
            overlayData.AlphaBlend = false;

            for (var i = 0; i < Datas.Length; i += 1)
            {
                var data1 = Datas[i];
                var data2 = Datas[(i + 1) % Datas.Length];

                Render(overlayData, data1, data2, Datas.Length == 1);
                Render(overlayData, data1);
            }

            //foreach (var border in BorderLines)
            //    border.Render(new OverlayData(overlayData.CameraInfo) { Color = Colors.Green });
            //NodeMarkupTool.RenderCircle(Center, new OverlayData(overlayData.CameraInfo) { Color = Colors.Red });
        }
    }
    public class SegmentSelection : Selection
    {
        protected override Vector3 Position => Id.GetSegment().m_middlePosition;
        protected override float HalfWidth => Id.GetSegment().Info.m_halfWidth;
        public SegmentSelection(ushort id) : base(id) { }

        protected override IEnumerable<Data> Calculate()
        {
            var segment = Id.GetSegment();

            var startData = new Data()
            {
                halfWidth = segment.Info.m_halfWidth.RoundToNearest(0.1f),
                dir = segment.m_startDirection.normalized,
                angle = segment.m_startDirection.AbsoluteAngle(),
            };

            segment.CalculateCorner(Id, true, true, true, out startData.leftPos, out startData.leftDir, out _);
            segment.CalculateCorner(Id, true, true, false, out startData.rightPos, out startData.rightDir, out _);

            yield return startData;

            var endData = new Data()
            {
                halfWidth = segment.Info.m_halfWidth.RoundToNearest(0.1f),
                dir = segment.m_endDirection.normalized,
                angle = segment.m_endDirection.AbsoluteAngle(),
            };

            segment.CalculateCorner(Id, true, false, true, out endData.leftPos, out endData.leftDir, out _);
            segment.CalculateCorner(Id, true, false, false, out endData.rightPos, out endData.rightDir, out _);

            yield return endData;
        }

        public override void Render(OverlayData overlayData)
        {
            overlayData.Width = OverlayWidth;
            overlayData.AlphaBlend = false;

            Render(overlayData, Datas[0], Datas[1]);
            Render(overlayData, Datas[0]);
            Render(overlayData, Datas[1]);
        }
    }
}
