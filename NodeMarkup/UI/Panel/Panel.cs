﻿using ColossalFramework.UI;
using ModsCommon.Utilities;
using NodeMarkup.Manager;
using NodeMarkup.Tools;
using NodeMarkup.UI.Editors;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using UnityEngine;

namespace NodeMarkup.UI.Panel
{
    public class NodeMarkupPanel : UIPanel
    {
        #region PROPERTIES

        public static NodeMarkupPanel Instance { get; private set; }
        private static Vector2 DefaultPosition { get; } = new Vector2(100f, 100f);

        public bool Active
        {
            set
            {
                enabled = value;
                isVisible = value;
            }
        }

        private float Width => 550f;

        protected NodeMarkupTool Tool => NodeMarkupTool.Instance;
        public Markup Markup { get; private set; }
        private bool NeedUpdateOnVisible { get; set; }

        private PanelHeader Header { get; set; }
        private PanelTabStrip TabStrip { get; set; }
        private UIPanel SizeChanger { get; set; }
        public List<Editor> Editors { get; } = new List<Editor>();
        public Editor CurrentEditor { get; set; }

        private Vector2 EditorSize => size - new Vector2(0, Header.height + TabStrip.height);
        private Vector2 EditorPosition => new Vector2(0, TabStrip.relativePosition.y + TabStrip.height);

        public bool Available
        {
            set
            {
                Header.Available = value;
                TabStrip.SetAvailable(value);
            }
        }

        #endregion

        #region BASIC

        public static void CreatePanel()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupPanel)}.{nameof(CreatePanel)}");
            UIView.GetAView().AddUIComponent(typeof(NodeMarkupPanel));
            Mod.Logger.Debug($"Panel created");
        }
        public override void Awake()
        {
            base.Awake();

            Instance = this;

            atlas = TextureHelper.InGameAtlas;
            backgroundSprite = "MenuPanel2";
            name = "NodeMarkupPanel";

            CreateHeader();
            CreateTabStrip();
            CreateEditors();
            CreateSizeChanger();

            size = new Vector2(Width, Header.height + TabStrip.height + 400);
            minimumSize = new Vector2(Width, Header.height + TabStrip.height + 200);

            Instance.Active = false;
        }
        public override void Start()
        {
            base.Start();
            SetPosition();
        }
        public override void OnEnable()
        {
            base.OnEnable();

            CheckPosition();
            UpdatePanel();
        }
        public static void RemovePanel()
        {
            Mod.Logger.Debug($"{nameof(NodeMarkupPanel)}.{nameof(RemovePanel)}");
            if (Instance != null)
            {
                Instance.Hide();
                Destroy(Instance);
                Instance = null;
                Mod.Logger.Debug($"Panel removed");
            }
        }
        private void CheckPosition()
        {
            if (absolutePosition.x < 0 || absolutePosition.y < 0)
                SetPosition();
        }
        private void SetPosition()
        {
            Mod.Logger.Debug($"Set default panel position");
            absolutePosition = DefaultPosition;
        }
        private new void Reset() => Available = true;

        #endregion

        #region UPDATE

        public void UpdatePanel()
        {
            Reset();
            CurrentEditor?.UpdateEditor();
        }
        private void UpdateOnVisible()
        {
            NeedUpdateOnVisible = false;

            Header.Text = Markup.PanelCaption;
            TabStrip.SetVisible(Markup);
            TabStrip.ArrangeTabs();
            TabStrip.SelectedTab = -1;
            SelectEditor<LinesEditor>();
        }

        #endregion

        #region COMPONENTS

        private void CreateHeader()
        {
            Header = AddUIComponent<PanelHeader>();
            Header.size = new Vector2(Width, 42);
            Header.relativePosition = new Vector2(0, 0);
            Header.target = parent;
        }
        private void CreateTabStrip()
        {
            TabStrip = AddUIComponent<PanelTabStrip>();
            TabStrip.relativePosition = new Vector3(0, Header.height);
            TabStrip.SelectedTabChanged += OnSelectedTabChanged;
            TabStrip.SelectedTab = -1;
        }
        private void CreateEditors()
        {
            CreateEditor<PointsEditor>();
            CreateEditor<LinesEditor>();
            CreateEditor<CrosswalksEditor>();
            CreateEditor<FillerEditor>();
            CreateEditor<StyleTemplateEditor>();
            CreateEditor<IntersectionTemplateEditor>();
        }
        private void CreateEditor<EditorType>() where EditorType : Editor
        {
            var editor = AddUIComponent<EditorType>();
            editor.Active = false;
            editor.Init(this);
            TabStrip.AddTab(editor);

            Editors.Add(editor);
        }
        private void CreateSizeChanger()
        {
            SizeChanger = AddUIComponent<UIPanel>();
            SizeChanger.size = new Vector2(9, 9);
            SizeChanger.atlas = TextureHelper.CommonAtlas;
            SizeChanger.backgroundSprite = TextureHelper.ResizeSprite;
            SizeChanger.color = new Color32(255, 255, 255, 160);
            SizeChanger.eventPositionChanged += SizeChangerPositionChanged;

            var handle = SizeChanger.AddUIComponent<UIDragHandle>();
            handle.size = SizeChanger.size;
            handle.relativePosition = Vector2.zero;
            handle.target = SizeChanger;
        }

        #endregion

        #region ONEVENTS

        private void SizeChangerPositionChanged(UIComponent component, Vector2 value)
        {
            size = (Vector2)SizeChanger.relativePosition + SizeChanger.size;
            SizeChanger.relativePosition = size - SizeChanger.size;
        }
        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();

            if (Header != null)
                Header.size = new Vector2(width, Header.height);
            if (TabStrip != null)
                TabStrip.width = width;
            if (CurrentEditor != null)
            {
                CurrentEditor.size = EditorSize;
                CurrentEditor.relativePosition = EditorPosition;
            }
            if (SizeChanger != null)
                SizeChanger.relativePosition = size - SizeChanger.size;
        }   
        protected override void OnVisibilityChanged()
        {
            base.OnVisibilityChanged();
            if (isVisible && NeedUpdateOnVisible)
                UpdateOnVisible();
        }           
        private void OnSelectedTabChanged(int index)
        {
            CurrentEditor = SelectEditor(index);
            UpdatePanel();
        }
        private Editor SelectEditor(int index)
        {
            if (index >= 0 && Editors.Count > index)
            {
                foreach (var editor in Editors)
                    editor.Active = false;

                var selectEditor = Editors[index];
                selectEditor.Active = true;
                selectEditor.size = EditorSize;
                selectEditor.relativePosition = EditorPosition;
                return selectEditor;
            }
            else
                return null;
        }
        private EditorType SelectEditor<EditorType>() where EditorType : Editor
        {
            var editorIndex = Editors.FindIndex((e) => e.GetType() == typeof(EditorType));
            TabStrip.SelectedTab = editorIndex;
            return Editors[editorIndex] as EditorType;
        }

        #endregion

        #region EDIT

        public void SetMarkup(Markup markup)
        {
            Markup = markup;
            if (Markup != null)
            {
                if (isVisible)
                    UpdateOnVisible();
                else
                    NeedUpdateOnVisible = true;
            }
        }

        private EditorType Edit<EditorType, ItemType>(ItemType item)
            where EditorType : Editor, IEditor<ItemType>
            where ItemType : ISupport
        {
            if (!(Markup is ISupport<ItemType>))
                return null;

            Reset();
            var editor = SelectEditor<EditorType>();
            editor?.Edit(item);
            return editor;
        }
        public void EditPoint(MarkupPoint point) => Edit<PointsEditor, MarkupPoint>(point);
        public void EditLine(MarkupLine line) => Edit<LinesEditor, MarkupLine>(line);
        public void EditCrosswalk(MarkupCrosswalk crosswalk)
        {
            var editor = Edit<CrosswalksEditor, MarkupCrosswalk>(crosswalk);
            editor?.BorderSetup();
        }
        public void EditFiller(MarkupFiller filler) => Edit<FillerEditor, MarkupFiller>(filler);

        private void EditTemplate<EditorType, TemplateType>(TemplateType template, bool editName)
            where EditorType : Editor, ITemplateEditor<TemplateType>
            where TemplateType : Template
        {
            var editor = Edit<EditorType, TemplateType>(template);
            if (editName && editor != null)
            {
                editor.EditName();
            }
        }

        public void EditStyleTemplate(StyleTemplate template, bool editName = true) => EditTemplate<StyleTemplateEditor, StyleTemplate>(template, editName);
        public void EditIntersectionTemplate(IntersectionTemplate template, bool editName = true) => EditTemplate<IntersectionTemplateEditor, IntersectionTemplate>(template, editName);

        public bool OnShortcut(Event e) => CurrentEditor?.OnShortcut(e) == true;
        public void Render(RenderManager.CameraInfo cameraInfo) => CurrentEditor?.Render(cameraInfo);

        #endregion
    }
}
