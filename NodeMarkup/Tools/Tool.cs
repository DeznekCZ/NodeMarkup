﻿using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using NodeMarkup.UI;
using NodeMarkup.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using NodeMarkup.Manager;
using ICities;
using ColossalFramework.PlatformServices;
using System.Xml.Linq;
using NodeMarkup.UI.Editors;
using NodeMarkup.UI.Panel;
using System.Diagnostics;
using ColossalFramework.Packaging;
using ColossalFramework.IO;
using System.IO;
using ColossalFramework.Importers;
using ModsCommon.Utilities;
using ModsCommon.UI;

namespace NodeMarkup.Tools
{
    public class NodeMarkupTool : ToolBase
    {
        #region PROPERTIES

        #region STATIC
        public static NodeMarkupShortcut DeleteAllShortcut { get; } = new NodeMarkupShortcut(nameof(DeleteAllShortcut), nameof(Localize.Settings_ShortcutDeleteAllNodeLines), SavedInputKey.Encode(KeyCode.D, true, true, false), () => Instance.DeleteAllMarking());
        public static NodeMarkupShortcut ResetOffsetsShortcut { get; } = new NodeMarkupShortcut(nameof(ResetOffsetsShortcut), nameof(Localize.Settings_ShortcutResetPointsOffset), SavedInputKey.Encode(KeyCode.R, true, true, false), () => Instance.ResetAllOffsets());
        public static NodeMarkupShortcut AddFillerShortcut { get; } = new NodeMarkupShortcut(nameof(AddFillerShortcut), nameof(Localize.Settings_ShortcutAddNewFiller), SavedInputKey.Encode(KeyCode.F, true, true, false), () => Instance.StartCreateFiller());
        public static NodeMarkupShortcut CopyMarkingShortcut { get; } = new NodeMarkupShortcut(nameof(CopyMarkingShortcut), nameof(Localize.Settings_ShortcutCopyMarking), SavedInputKey.Encode(KeyCode.C, true, true, false), () => Instance.CopyMarkup());
        public static NodeMarkupShortcut PasteMarkingShortcut { get; } = new NodeMarkupShortcut(nameof(PasteMarkingShortcut), nameof(Localize.Settings_ShortcutPasteMarking), SavedInputKey.Encode(KeyCode.V, true, true, false), () => Instance.PasteMarkup());
        public static NodeMarkupShortcut EditMarkingShortcut { get; } = new NodeMarkupShortcut(nameof(EditMarkingShortcut), nameof(Localize.Settings_ShortcutEditMarking), SavedInputKey.Encode(KeyCode.E, true, true, false), () => Instance.EditMarkup());
        public static NodeMarkupShortcut CreateEdgeLinesShortcut { get; } = new NodeMarkupShortcut(nameof(CreateEdgeLinesShortcut), nameof(Localize.Settings_ShortcutCreateEdgeLines), SavedInputKey.Encode(KeyCode.W, true, true, false), () => Instance.CreateEdgeLines());
        public static NodeMarkupShortcut ActivationShortcut { get; } = new NodeMarkupShortcut(nameof(ActivationShortcut), nameof(Localize.Settings_ShortcutActivateTool), SavedInputKey.Encode(KeyCode.L, true, false, false));
        public static NodeMarkupShortcut AddRuleShortcut { get; } = new NodeMarkupShortcut(nameof(AddRuleShortcut), nameof(Localize.Settings_ShortcutAddNewLineRule), SavedInputKey.Encode(KeyCode.A, true, true, false));
        public static NodeMarkupShortcut SaveAsIntersectionTemplateShortcut { get; } = new NodeMarkupShortcut(nameof(SaveAsIntersectionTemplateShortcut), nameof(Localize.Settings_ShortcutSaveAsPreset), SavedInputKey.Encode(KeyCode.S, true, true, false), () => Instance.SaveAsIntersectionTemplate());
        public static NodeMarkupShortcut CutLinesByCrosswalks { get; } = new NodeMarkupShortcut(nameof(CutLinesByCrosswalks), nameof(Localize.Settings_ShortcutCutLinesByCrosswalks), SavedInputKey.Encode(KeyCode.T, true, true, false), () => Instance.CutByCrosswalks());

        public static IEnumerable<NodeMarkupShortcut> Shortcuts
        {
            get
            {
                yield return DeleteAllShortcut;
                yield return ResetOffsetsShortcut;
                yield return AddFillerShortcut;
                yield return CopyMarkingShortcut;
                yield return PasteMarkingShortcut;
                yield return EditMarkingShortcut;
                yield return CreateEdgeLinesShortcut;
                yield return SaveAsIntersectionTemplateShortcut;
                yield return CutLinesByCrosswalks;
            }
        }

        public static Dictionary<Style.StyleType, SavedInt> StylesModifier { get; } = EnumExtension.GetEnumValues<Style.StyleType>().ToDictionary(i => i, i => new SavedInt($"{nameof(StylesModifier)}{(int)(object)i}", Settings.SettingsFile, (int)GetDefaultStylesModifier(i), true));

        public static Ray MouseRay { get; private set; }
        public static float MouseRayLength { get; private set; }
        public static bool MouseRayValid { get; private set; }
        public static Vector3 MousePosition { get; private set; }
        public static Vector3 MouseWorldPosition { get; private set; }
        public static Vector3 CameraDirection { get; private set; }

        #endregion

        public BaseToolMode Mode { get; private set; }
        public BaseToolMode NextMode { get; private set; }
        public ToolModeType ModeType => Mode?.Type ?? ToolModeType.None;
        private Dictionary<ToolModeType, BaseToolMode> ToolModes { get; set; } = new Dictionary<ToolModeType, BaseToolMode>();
        public Markup Markup { get; private set; }

        public static RenderManager RenderManager => Singleton<RenderManager>.instance;

        private NodeMarkupButton Button => NodeMarkupButton.Instance;
        private NodeMarkupPanel Panel => NodeMarkupPanel.Instance;
        private ToolBase PrevTool { get; set; }
        private UIComponent PauseMenu { get; } = UIView.library.Get("PauseMenu");
        public IntersectionTemplate MarkupBuffer { get; private set; }

        #endregion

        #region BASIC
        public static NodeMarkupTool Instance { get; set; }
        protected override void Awake()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(Awake)}");
            base.Awake();

            Instance = this;
            Mod.Logger.Debug($"Tool Created");

            ToolModes = new Dictionary<ToolModeType, BaseToolMode>()
            {
                { ToolModeType.Select, Instance.CreateToolMode<SelectToolMode>() },
                { ToolModeType.MakeLine, Instance.CreateToolMode<MakeLineToolMode>() },
                { ToolModeType.MakeCrosswalk, Instance.CreateToolMode<MakeCrosswalkToolMode>() },
                { ToolModeType.MakeFiller, Instance.CreateToolMode<MakeFillerToolMode>() },
                { ToolModeType.DragPoint, Instance.CreateToolMode<DragPointToolMode>() },
                { ToolModeType.PasteEntersOrder, Instance.CreateToolMode<PasteEntersOrderToolMode>()},
                { ToolModeType.EditEntersOrder, Instance.CreateToolMode<EditEntersOrderToolMode>()},
                { ToolModeType.ApplyIntersectionTemplateOrder, Instance.CreateToolMode<ApplyIntersectionTemplateOrderToolMode>()},
                { ToolModeType.PointsOrder, Instance.CreateToolMode<PointsOrderToolMode>()},
            };

            NodeMarkupButton.CreateButton();
            NodeMarkupPanel.CreatePanel();

            enabled = false;
        }
        public Mode CreateToolMode<Mode>() where Mode : BaseToolMode => gameObject.AddComponent<Mode>();
        public static void Create()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(Create)}");
            ToolsModifierControl.toolController.gameObject.AddComponent<NodeMarkupTool>();
        }
        public static void Remove()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(Remove)}");
            if (Instance != null)
            {
                Destroy(Instance);
                Instance = null;
                Mod.Logger.Debug($"Tool removed");
            }
        }
        protected override void OnDestroy()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(OnDestroy)}");
            NodeMarkupButton.RemoveButton();
            NodeMarkupPanel.RemovePanel();
            ComponentPool.Clear();
            base.OnDestroy();
        }
        protected override void OnEnable()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(OnEnable)}");
            Reset();

            PrevTool = m_toolController.CurrentTool;

            base.OnEnable();

            Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.Default);
        }
        protected override void OnDisable()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(OnDisable)}");
            Reset();

            if (m_toolController?.NextTool == null && PrevTool != null)
                PrevTool.enabled = true;
            else
                ToolsModifierControl.SetTool<DefaultTool>();

            PrevTool = null;
        }
        private void Reset()
        {
            SetModeNow(ToolModeType.Select);
            cursorInfoLabel.isVisible = false;
            cursorInfoLabel.text = string.Empty;
        }

        public void ToggleTool()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(ToggleTool)}: {(!enabled ? "enable" : "disable")}");
            enabled = !enabled;
        }
        public void Disable() => enabled = false;

        public void SetDefaultMode() => SetMode(ToolModeType.MakeLine);
        public void SetMode(ToolModeType mode) => SetMode(ToolModes[mode]);
        public void SetMode(BaseToolMode mode)
        {
            if (Mode != mode)
                NextMode = mode;
        }
        private void SetModeNow(ToolModeType mode) => SetModeNow(ToolModes[mode]);
        private void SetModeNow(BaseToolMode mode)
        {
            Mode?.Deactivate();
            var prevMode = Mode;
            Mode = mode;
            Mode?.Activate(prevMode);

            Panel.Active = Mode?.ShowPanel == true;
        }

        public void SetMarkup(Markup markup)
        {
            Markup = markup;
            Panel.SetMarkup(Markup);
        }
        #endregion

        #region UPDATE
        protected override void OnToolUpdate()
        {
            if (NextMode != null)
            {
                SetModeNow(NextMode);
                NextMode = null;
            }

            if (PauseMenu?.isVisible == true)
            {
                PrevTool = null;
                Disable();
                UIView.library.Hide("PauseMenu");
                return;
            }
            if ((RenderManager.CurrentCameraInfo.m_layerMask & (3 << 24)) == 0)
            {
                PrevTool = null;
                Disable();
                return;
            }

            MousePosition = Input.mousePosition;
            MouseRay = Camera.main.ScreenPointToRay(MousePosition);
            MouseRayLength = Camera.main.farClipPlane;
            MouseRayValid = !UIView.IsInsideUI() && Cursor.visible;
            RaycastInput input = new RaycastInput(MouseRay, MouseRayLength);
            RayCast(input, out RaycastOutput output);
            MouseWorldPosition = output.m_hitPos;
            var cameraDirection = Vector3.forward.TurnDeg(Camera.main.transform.eulerAngles.y, true);
            cameraDirection.y = 0;
            CameraDirection = cameraDirection.normalized;

            Mode.OnToolUpdate();
            Info();

            base.OnToolUpdate();
        }

        #region INFO

        private void Info()
        {
            var position = GetInfoPosition();

            var isToolTipEnable = Settings.ShowToolTip || Mode.Type == ToolModeType.Select;
            var isPanelHover = Panel.isVisible && new Rect(Panel.relativePosition, Panel.size).Contains(position);
            var isHasText = Mode.GetToolInfo() is string info && !string.IsNullOrEmpty(info);

            if (isToolTipEnable && !isPanelHover && isHasText)
                ShowToolInfo(Mode.GetToolInfo(), position);
            else
                cursorInfoLabel.isVisible = false;
        }
        private void ShowToolInfo(string text, Vector3 relativePosition)
        {
            if (cursorInfoLabel == null)
                return;

            cursorInfoLabel.isVisible = true;
            cursorInfoLabel.text = text ?? string.Empty;

            UIView uIView = cursorInfoLabel.GetUIView();

            relativePosition += new Vector3(25, 25);

            var screenSize = fullscreenContainer?.size ?? uIView.GetScreenResolution();
            relativePosition.x = MathPos(relativePosition.x, cursorInfoLabel.width, screenSize.x);
            relativePosition.y = MathPos(relativePosition.y, cursorInfoLabel.height, screenSize.y);

            cursorInfoLabel.relativePosition = relativePosition;

            static float MathPos(float pos, float size, float screen) => pos + size > screen ? (screen - size < 0 ? 0 : screen - size) : Mathf.Max(pos, 0);
        }
        private Vector3 GetInfoPosition()
        {
            var uiView = cursorInfoLabel.GetUIView();
            var mouse = uiView.ScreenPointToGUI(MousePosition / uiView.inputScale);

            return mouse;
        }

        #endregion

        #endregion

        #region GUI

        private bool IsMouseDown { get; set; }
        private bool IsMouseMove { get; set; }
        protected override void OnToolGUI(Event e)
        {
            Mode.OnToolGUI(e);

            if (Shortcuts.Any(s => s.IsPressed(e)) || Panel?.OnShortcut(e) == true)
                return;

            switch (e.type)
            {
                case EventType.MouseDown when MouseRayValid && e.button == 0:
                    IsMouseDown = true;
                    IsMouseMove = false;
                    Mode.OnMouseDown(e);
                    break;
                case EventType.MouseDrag when MouseRayValid:
                    IsMouseMove = true;
                    Mode.OnMouseDrag(e);
                    break;
                case EventType.MouseUp when MouseRayValid && e.button == 0:
                    if (IsMouseMove)
                        Mode.OnMouseUp(e);
                    else
                        Mode.OnPrimaryMouseClicked(e);
                    IsMouseDown = false;
                    break;
                case EventType.MouseUp when MouseRayValid && e.button == 1:
                    Mode.OnSecondaryMouseClicked();
                    break;
            }
        }
        private void StartCreateFiller()
        {
            if (Markup is ISupportFillers)
            {
                SetMode(ToolModeType.MakeFiller);
                if (NextMode is MakeFillerToolMode fillerToolMode)
                    fillerToolMode.DisableByAlt = false;
            }
        }
        private void DeleteAllMarking()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(DeleteAllMarking)}");

            var messageBox = MessageBoxBase.ShowModal<YesNoMessageBox>();
            messageBox.CaprionText = Localize.Tool_ClearMarkingsCaption;
            messageBox.MessageText = string.Format($"{Localize.Tool_ClearMarkingsMessage}\n{NodeMarkupMessageBox.CantUndone}", Markup.Id);
            messageBox.OnButton1Click = Delete;

            bool Delete()
            {
                Markup.Clear();
                Panel.UpdatePanel();
                return true;
            }
        }
        private void ResetAllOffsets()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(ResetAllOffsets)}");

            if (Settings.DeleteWarnings)
            {
                var messageBox = MessageBoxBase.ShowModal<YesNoMessageBox>();
                messageBox.CaprionText = Localize.Tool_ResetOffsetsCaption;
                messageBox.MessageText = $"{string.Format(Localize.Tool_ResetOffsetsMessage, Markup.Id)}\n{NodeMarkupMessageBox.CantUndone}";
                messageBox.OnButton1Click = Reset;
            }
            else
                Reset();

            bool Reset()
            {
                Markup.ResetOffsets();
                Panel.UpdatePanel();
                return true;
            }
        }
        public void DeleteItem(IDeletable item, Action onDelete)
        {
            if (Settings.DeleteWarnings)
            {
                var dependences = item.GetDependences();
                if (dependences.Exist)
                {
                    ShowModal(GetDeleteDependences(dependences));
                    return;
                }
                else if (Settings.DeleteWarningsType == 0)
                {
                    ShowModal(string.Empty);
                    return;
                }
            }

            onDelete();

            void ShowModal(string additional)
            {
                var messageBox = MessageBoxBase.ShowModal<YesNoMessageBox>();
                messageBox.CaprionText = string.Format(Localize.Tool_DeleteCaption, item.DeleteCaptionDescription);
                messageBox.MessageText = $"{string.Format(Localize.Tool_DeleteMessage, item.DeleteMessageDescription, item)}\n{NodeMarkupMessageBox.CantUndone}\n\n{additional}";
                messageBox.OnButton1Click = () =>
                    {
                        onDelete();
                        return true;
                    };
            }
        }
        private string GetDeleteDependences(Dependences dependences)
        {
            var strings = dependences.Total.Where(i => i.Value > 0).Select(i => string.Format(i.Key.Description(), i.Value)).ToArray();
            return $"{Localize.Tool_DeleteDependence}\n{string.Join(", ", strings)}.";
        }

        private void CopyMarkup()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(CopyMarkup)}");
            MarkupBuffer = new IntersectionTemplate(Markup);
        }
        private void PasteMarkup()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(PasteMarkup)}");

            if (Settings.DeleteWarnings)
            {
                var messageBox = MessageBoxBase.ShowModal<YesNoMessageBox>();
                messageBox.CaprionText = Localize.Tool_PasteMarkingsCaption;
                messageBox.MessageText = $"{Localize.Tool_PasteMarkingsMessage}\n{NodeMarkupMessageBox.CantUndone}";
                messageBox.OnButton1Click = Paste;
            }
            else
                Paste();

            bool Paste()
            {
                BaseOrderToolMode.IntersectionTemplate = MarkupBuffer;
                SetMode(ToolModeType.PasteEntersOrder);
                return true;
            }
        }
        private void EditMarkup()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(EditMarkup)}");

            BaseOrderToolMode.IntersectionTemplate = new IntersectionTemplate(Markup);
            SetMode(ToolModeType.EditEntersOrder);
        }
        public void ApplyIntersectionTemplate(IntersectionTemplate template)
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(ApplyIntersectionTemplate)}");

            BaseOrderToolMode.IntersectionTemplate = template;
            SetMode(ToolModeType.ApplyIntersectionTemplateOrder);
        }
        private void CreateEdgeLines()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(CreateEdgeLines)}");

            var lines = Markup.Enters.Select(e => Markup.AddConnection(new MarkupPointPair(e.LastPoint, e.Next.FirstPoint), Style.StyleType.EmptyLine)).ToArray();
            Panel.EditLine(lines.Last());
        }
        private void SaveAsIntersectionTemplate()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(SaveAsIntersectionTemplate)}");

            StartCoroutine(MakeScreenshot(Callback));

            void Callback(Image image)
            {
                if (TemplateManager.IntersectionManager.AddTemplate(Markup, image, out IntersectionTemplate template))
                    Panel.EditIntersectionTemplate(template);
            }
        }
        private void CutByCrosswalks()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(CutByCrosswalks)}");

            foreach (var crosswalk in Markup.Crosswalks)
                Markup.CutLinesByCrosswalk(crosswalk);
        }
        private int ScreenshotSize => 400;
        private IEnumerator MakeScreenshot(Action<Image> callback)
        {
            if (callback == null)
                yield break;

            Mod.Logger.Debug($"{nameof(NodeMarkupTool)}.{nameof(MakeScreenshot)}");

            var cameraController = ToolsModifierControl.cameraController;
            var camera = Camera.main;
            var backupMask = camera.cullingMask;
            var backupRect = camera.rect;
            var backupPosition = cameraController.m_currentPosition;
            var backupRotation = cameraController.m_currentAngle;
            var backupSize = cameraController.m_currentSize;

            var angle = GetCameraAngle();
            GetCameraPorition(angle, out Vector3 position, out float size);
            SetCameraPosition(position, new Vector2(0f, 90f), size);

            yield return new WaitForEndOfFrame();

            camera.transform.position = position + new Vector3(0, Math.Max(size * 1.1f, size + 5f) / 2 / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2), 0);
            camera.transform.rotation = Quaternion.Euler(90, (2 * Mathf.PI - angle - Vector3.forward.AbsoluteAngle()) * Mathf.Rad2Deg, 0);
            camera.cullingMask = LayerMask.GetMask("Road") | (3 << 24);
            camera.rect = new Rect(0f, 0f, 1f, 1f);

            bool smaaEnabled = false;
            var smaa = camera.GetComponent<SMAA>();
            if (smaa != null)
            {
                smaaEnabled = smaa.enabled;
                smaa.enabled = true;
            }

            var scale = ScreenshotSize * 4;

            camera.targetTexture = new RenderTexture(scale, scale, 24);
            var screenShot = new Texture2D(scale, scale, TextureFormat.RGB24, false);

            Singleton<RenderManager>.instance.UpdateCameraInfo();
            camera.Render();

            if (smaa != null)
                smaa.enabled = smaaEnabled;

            RenderTexture.active = camera.targetTexture;
            screenShot.ReadPixels(new Rect(0, 0, scale, scale), 0, 0);
            RenderTexture.active = null;
            Destroy(camera.targetTexture);

            SetCameraPosition(backupPosition, backupRotation, backupSize);
            camera.targetTexture = null;
            camera.cullingMask = backupMask;
            camera.rect = backupRect;

            var data = screenShot.GetPixels32();
            var image = new Image(scale, scale, TextureFormat.RGB24, data);
            image.Resize(ScreenshotSize, ScreenshotSize);

            callback(image);
        }
        private float GetCameraAngle()
        {
            var enters = Markup.Enters.ToArray();

            switch (enters.Length)
            {
                case 0: return 0;
                case 1: return enters[0].NormalAngle;
                default:
                    var sortEnters = enters.OrderBy(e => e.RoadHalfWidth).Reverse().ToArray();
                    var selectWidth = sortEnters[1].RoadHalfWidth * 0.9f;
                    var selectEnters = sortEnters.Where(e => e.RoadHalfWidth > selectWidth).ToArray();

                    var first = 0;
                    var second = 1;
                    var maxDelta = 0f;

                    for (var i = 0; i < selectEnters.Length; i += 1)
                    {
                        for (var j = i + 1; j < selectEnters.Length; j += 1)
                        {
                            var delte = Mathf.Abs(selectEnters[i].NormalAngle - selectEnters[j].NormalAngle);
                            if (delte > Mathf.PI)
                                delte = 2 * Mathf.PI - delte;
                            if (delte > maxDelta)
                            {
                                maxDelta = delte;
                                first = i;
                                second = j;
                            }
                        }
                    }

                    return (selectEnters[first].NormalAngle + selectEnters[second].NormalAngle) / 2;
            }
        }
        private void GetCameraPorition(float angle, out Vector3 position, out float size)
        {
            var points = Markup.Enters.SelectMany(e => new Vector3[] { e.FirstPointSide, e.LastPointSide }).ToArray();

            if (!points.Any())
            {
                position = Markup.Position;
                size = 10f;
                return;
            }

            var dir = angle.Direction();
            var normal = dir.Turn90(false);

            var rect = new Rect();
            foreach (var point in points)
            {
                Line2.Intersect(Markup.Position.XZ(), (Markup.Position + dir).XZ(), point.XZ(), (point + normal).XZ(), out float x, out _);
                Line2.Intersect(Markup.Position.XZ(), (Markup.Position + normal).XZ(), point.XZ(), (point + dir).XZ(), out float y, out _);

                Set(ref rect, x, y);
            }

            position = Markup.Position + dir * rect.center.x + normal * rect.center.y;
            size = Mathf.Max(rect.width, rect.height);

            static void Set(ref Rect rect, float x, float y)
            {
                if (x < rect.xMin)
                    rect.xMin = x;
                else if (x > rect.xMax)
                    rect.xMax = x;

                if (y < rect.yMin)
                    rect.yMin = y;
                else if (y > rect.yMax)
                    rect.yMax = y;
            }
        }
        private void SetCameraPosition(Vector3 position, Vector2 rotation, float size)
        {
            var cameraController = ToolsModifierControl.cameraController;
            cameraController.ClearTarget();
            cameraController.SetOverrideModeOff();
            cameraController.m_targetPosition = cameraController.m_currentPosition = position;
            cameraController.m_targetAngle = cameraController.m_currentAngle = rotation;
            cameraController.m_targetSize = cameraController.m_currentSize = size;
        }

        #endregion

        #region OVERLAY

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            Mode.RenderOverlay(cameraInfo);
            base.RenderOverlay(cameraInfo);
        }

        private static float DefaultWidth => 0.2f;
        private static bool DefaultBlend => true;
        public static void RenderBezier(RenderManager.CameraInfo cameraInfo, Bezier3 bezier, Color? color = null, float? width = null, bool? alphaBlend = null, bool? cut = null)
        {
            var cutValue = cut == true ? (width ?? DefaultWidth) / 2 : 0f;
            RenderManager.OverlayEffect.DrawBezier(cameraInfo, color ?? Colors.White, bezier, width ?? DefaultWidth, cutValue, cutValue, -1f, 1280f, false, alphaBlend ?? DefaultBlend);
        }

        public static void RenderCircle(RenderManager.CameraInfo cameraInfo, Vector3 position, Color? color = null, float? width = null, bool? alphaBlend = null) =>
            RenderManager.OverlayEffect.DrawCircle(cameraInfo, color ?? Colors.White, position, width ?? DefaultWidth, -1f, 1280f, false, alphaBlend ?? DefaultBlend);

        #endregion

        public static new bool RayCast(RaycastInput input, out RaycastOutput output) => ToolBase.RayCast(input, out output);

        public static Style.StyleType GetStyle<StyleType>(StyleType defaultStyle)
           where StyleType : Enum
        {
            var modifier = EnumExtension.GetEnumValues<StyleModifier>().FirstOrDefault(i => i.GetAttr<InputKeyAttribute, StyleModifier>() is InputKeyAttribute ik && ik.Control == InputExtension.CtrlIsPressed && ik.Shift == InputExtension.ShiftIsPressed && ik.Alt == InputExtension.AltIsPressed);

            foreach (var style in EnumExtension.GetEnumValues<StyleType>())
            {
                var general = (Style.StyleType)(object)style;
                if (StylesModifier.TryGetValue(general, out SavedInt saved) && (StyleModifier)saved.value == modifier)
                    return general;
            }
            return (Style.StyleType)(object)defaultStyle;
        }
        private static StyleModifier GetDefaultStylesModifier(Style.StyleType style)
        {
            return style switch
            {
                Style.StyleType.LineDashed => StyleModifier.Without,
                Style.StyleType.LineSolid => StyleModifier.Shift,
                Style.StyleType.LineDoubleDashed => StyleModifier.Ctrl,
                Style.StyleType.LineDoubleSolid => StyleModifier.CtrlShift,
                Style.StyleType.StopLineSolid => StyleModifier.Without,
                Style.StyleType.CrosswalkZebra => StyleModifier.Without,
                Style.StyleType.FillerStripe => StyleModifier.Without,
                _ => StyleModifier.NotSet,
            };
        }

        public static void GetCentreAndRadius(Markup markup, out Vector3 centre, out float radius)
        {
            var points = markup.Enters.Where(e => e.Position != null).SelectMany(e => new Vector3[] { e.FirstPointSide, e.LastPointSide }).ToArray();

            if (points.Length == 0)
            {
                centre = markup.Position;
                radius = markup.Radius;
                return;
            }

            centre = markup.Position;
            radius = 1000f;

            for (var i = 0; i < points.Length; i += 1)
            {
                for (var j = i + 1; j < points.Length; j += 1)
                {
                    GetCircle2Points(points, i, j, ref centre, ref radius);

                    for (var k = j + 1; k < points.Length; k += 1)
                        GetCircle3Points(points, i, j, k, ref centre, ref radius);
                }
            }

            radius += TargetEnter.Size / 2;
        }
        private static void GetCircle2Points(Vector3[] points, int i, int j, ref Vector3 centre, ref float radius)
        {
            var newCentre = (points[i] + points[j]) / 2;
            var newRadius = (points[i] - points[j]).magnitude / 2;

            if (newRadius >= radius)
                return;

            if (AllPointsInCircle(points, newCentre, newRadius, i, j))
            {
                centre = newCentre;
                radius = newRadius;
            }
        }
        private static void GetCircle3Points(Vector3[] points, int i, int j, int k, ref Vector3 centre, ref float radius)
        {
            var pos1 = (points[i] + points[j]) / 2;
            var pos2 = (points[j] + points[k]) / 2;

            var dir1 = (points[i] - points[j]).Turn90(true).normalized;
            var dir2 = (points[j] - points[k]).Turn90(true).normalized;

            Line2.Intersect(pos1.XZ(), (pos1 + dir1).XZ(), pos2.XZ(), (pos2 + dir2).XZ(), out float p, out _);
            var newCentre = pos1 + dir1 * p;
            var newRadius = (newCentre - points[i]).magnitude;

            if (newRadius >= radius)
                return;

            if (AllPointsInCircle(points, newCentre, newRadius, i, j, k))
            {
                centre = newCentre;
                radius = newRadius;
            }
        }
        private static bool AllPointsInCircle(Vector3[] points, Vector3 centre, float radius, params int[] ignore)
        {
            for (var i = 0; i < points.Length; i += 1)
            {
                if (ignore.Any(j => j == i))
                    continue;

                if ((centre - points[i]).magnitude > radius)
                    return false;
            }

            return true;
        }
    }
    public class ThreadingExtension : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if (!UIView.HasModalInput() && !UIView.HasInputFocus() && NodeMarkupTool.ActivationShortcut.InputKey.IsKeyUp())
                NodeMarkupTool.Instance.ToggleTool();
        }
    }
}
