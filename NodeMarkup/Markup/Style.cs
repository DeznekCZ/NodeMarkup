﻿using ColossalFramework.UI;
using ModsCommon.UI;
using ModsCommon.Utilities;
using NodeMarkup.UI;
using NodeMarkup.UI.Editors;
using NodeMarkup.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace NodeMarkup.Manager
{
    public interface IStyle { }
    public interface IColorStyle : IStyle
    {
        PropertyColorValue Color { get; }
    }
    public interface IWidthStyle : IStyle
    {
        PropertyValue<float> Width { get; }
    }
    public abstract class Style : IToXml
    {
        public static bool FromXml<T>(XElement config, ObjectsMap map, bool invert, out T style) where T : Style
        {
            var type = IntToType(config.GetAttrValue<int>("T"));

            if (TemplateManager.StyleManager.GetDefault<T>(type) is T defaultStyle)
            {
                style = defaultStyle;
                style.FromXml(config, map, invert);
                return true;
            }
            else
            {
                style = default;
                return false;
            }
        }
        private static StyleType IntToType(int rawType)
        {
            var typeGroup = rawType & (int)StyleType.GroupMask;
            var typeNum = (rawType & (int)StyleType.ItemMask) + 1;
            var type = (StyleType)((typeGroup == 0 ? (int)StyleType.RegularLine : typeGroup << 1) + typeNum);
            return type;
        }
        private static int TypeToInt(StyleType type)
        {
            var typeGroup = (int)type & (int)StyleType.GroupMask;
            var typeNum = ((int)type & (int)StyleType.ItemMask) - 1;
            var rawType = ((typeGroup >> 1) & (int)StyleType.GroupMask) + typeNum;
            return rawType;
        }

        private static int ColorVersion { get; } = 1;
        public static Color32 DefaultColor { get; } = new Color32(136, 136, 136, 224);
        public static float DefaultWidth { get; } = 0.15f;

        protected virtual float WidthWheelStep { get; } = 0.01f;
        protected virtual float WidthMinValue { get; } = 0.05f;

        public static T GetDefault<T>(StyleType type) where T : Style
        {
            return (type & StyleType.GroupMask) switch
            {
                StyleType.RegularLine when RegularLineStyle.GetDefault((RegularLineStyle.RegularLineType)(int)type) is T tStyle => tStyle,
                StyleType.StopLine when StopLineStyle.GetDefault((StopLineStyle.StopLineType)(int)type) is T tStyle => tStyle,
                StyleType.Filler when FillerStyle.GetDefault((FillerStyle.FillerType)(int)type) is T tStyle => tStyle,
                StyleType.Crosswalk when CrosswalkStyle.GetDefault((CrosswalkStyle.CrosswalkType)(int)type) is T tStyle => tStyle,
                _ => null,
            };
        }

        public static string XmlName { get; } = "S";

        public Action OnStyleChanged { private get; set; }
        public string XmlSection => XmlName;
        public abstract StyleType Type { get; }

        protected virtual void StyleChanged() => OnStyleChanged?.Invoke();

        public PropertyColorValue Color { get; }
        public PropertyValue<float> Width { get; }
        public Style(Color32 color, float width)
        {
            Color = GetColorProperty(color);
            Width = GetWidthProperty(width);
        }
        protected XElement BaseToXml() => new XElement(XmlSection, new XAttribute("T", TypeToInt(Type)));
        public virtual XElement ToXml()
        {
            var config = BaseToXml();
            config.Add(Color.ToXml());
            config.Add(new XAttribute("CV", ColorVersion));
            config.Add(Width.ToXml());
            return config;
        }
        public virtual void FromXml(XElement config, ObjectsMap map, bool invert)
        {
            //var colorVersion = config.GetAttrValue<int>("CV");
            Color.FromXml(config, DefaultColor);
            Width.FromXml(config, DefaultWidth);
        }

        public abstract Style Copy();
        public virtual void CopyTo(Style target)
        {
            if (this is IWidthStyle widthSource && target is IWidthStyle widthTarget)
                widthTarget.Width.Value = widthSource.Width;
            if (this is IColorStyle colorSource && target is IColorStyle colorTarget)
                colorTarget.Color.Value = colorSource.Color;
        }

        public virtual List<EditorItem> GetUIComponents(object editObject, UIComponent parent, Action onHover = null, Action onLeave = null, bool isTemplate = false)
        {
            var components = new List<EditorItem>();

            if (this is IColorStyle)
                components.Add(AddColorProperty(parent));
            if (this is IWidthStyle)
                components.Add(AddWidthProperty(parent, onHover, onLeave));

            return components;
        }
        protected ColorAdvancedPropertyPanel AddColorProperty(UIComponent parent)
        {
            var colorProperty = ComponentPool.Get<ColorAdvancedPropertyPanel>(parent);
            colorProperty.Text = Localize.StyleOption_Color;
            colorProperty.Init();
            colorProperty.Value = Color;
            colorProperty.OnValueChanged += (Color32 color) => Color.Value = color;
            return colorProperty;
        }
        protected FloatPropertyPanel AddWidthProperty(UIComponent parent, Action onHover, Action onLeave)
        {
            var widthProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            widthProperty.Text = Localize.StyleOption_Width;
            widthProperty.UseWheel = true;
            widthProperty.WheelStep = WidthWheelStep;
            widthProperty.CheckMin = true;
            widthProperty.MinValue = WidthMinValue;
            widthProperty.Init();
            widthProperty.Value = Width;
            widthProperty.OnValueChanged += (float value) => Width.Value = value;
            AddOnHoverLeave(widthProperty, onHover, onLeave);

            return widthProperty;
        }
        protected static FloatPropertyPanel AddDashLengthProperty(IDashedLine dashedStyle, UIComponent parent, Action onHover, Action onLeave)
        {
            var dashLengthProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            dashLengthProperty.Text = Localize.StyleOption_DashedLength;
            dashLengthProperty.UseWheel = true;
            dashLengthProperty.WheelStep = 0.1f;
            dashLengthProperty.CheckMin = true;
            dashLengthProperty.MinValue = 0.1f;
            dashLengthProperty.Init();
            dashLengthProperty.Value = dashedStyle.DashLength;
            dashLengthProperty.OnValueChanged += (float value) => dashedStyle.DashLength.Value = value;
            AddOnHoverLeave(dashLengthProperty, onHover, onLeave);
            return dashLengthProperty;
        }
        protected static FloatPropertyPanel AddSpaceLengthProperty(IDashedLine dashedStyle, UIComponent parent, Action onHover, Action onLeave)
        {
            var spaceLengthProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            spaceLengthProperty.Text = Localize.StyleOption_SpaceLength;
            spaceLengthProperty.UseWheel = true;
            spaceLengthProperty.WheelStep = 0.1f;
            spaceLengthProperty.CheckMin = true;
            spaceLengthProperty.MinValue = 0.1f;
            spaceLengthProperty.Init();
            spaceLengthProperty.Value = dashedStyle.SpaceLength;
            spaceLengthProperty.OnValueChanged += (float value) => dashedStyle.SpaceLength.Value = value;
            AddOnHoverLeave(spaceLengthProperty, onHover, onLeave);
            return spaceLengthProperty;
        }
        protected static ButtonsPanel AddInvertProperty(IAsymLine asymStyle, UIComponent parent)
        {
            var buttonsPanel = ComponentPool.Get<ButtonsPanel>(parent);
            var invertIndex = buttonsPanel.AddButton(Localize.StyleOption_Invert);
            buttonsPanel.Init();
            buttonsPanel.OnButtonClick += OnButtonClick;

            void OnButtonClick(int index)
            {
                if (index == invertIndex)
                    asymStyle.Invert.Value = !asymStyle.Invert;
            }

            return buttonsPanel;
        }
        protected static void AddOnHoverLeave<ValueType, FieldType>(FieldPropertyPanel<ValueType, FieldType> fieldPanel, Action onHover, Action onLeave)
            where FieldType : UITextField<ValueType>
        {
            if (onHover != null)
                fieldPanel.OnHover += onHover;
            if (onLeave != null)
                fieldPanel.OnLeave += onLeave;
        }

        protected PropertyColorValue GetColorProperty(Color32 defaultValue) => new PropertyColorValue("C", StyleChanged, defaultValue);
        protected PropertyValue<float> GetWidthProperty(float defaultValue) => new PropertyValue<float>("W", StyleChanged, defaultValue);
        protected PropertyValue<float> GetOffsetProperty(float defaultValue) => new PropertyValue<float>("O", StyleChanged, defaultValue);
        protected PropertyValue<float> GetMedianOffsetProperty(float defaultValue) => new PropertyValue<float>("MO", StyleChanged, defaultValue);
        protected string AlignmentLabel => "A";
        protected PropertyEnumValue<LineStyle.StyleAlignment> GetAlignmentProperty(LineStyle.StyleAlignment defaultValue) => new PropertyEnumValue<LineStyle.StyleAlignment>(AlignmentLabel, StyleChanged, defaultValue);
        protected PropertyValue<float> GetDashLengthProperty(float defaultValue) => new PropertyValue<float>("DL", StyleChanged, defaultValue);
        protected PropertyValue<float> GetSpaceLengthProperty(float defaultValue) => new PropertyValue<float>("SL", StyleChanged, defaultValue);
        protected PropertyBoolValue GetInvertProperty(bool defaultValue) => new PropertyBoolValue("I", StyleChanged, defaultValue);
        protected PropertyBoolValue GetCenterSolidProperty(bool defaultValue) => new PropertyBoolValue("CS", StyleChanged, defaultValue);
        protected PropertyValue<float> GetBaseProperty(float defaultValue) => new PropertyValue<float>("B", StyleChanged, defaultValue);
        protected PropertyValue<float> GetHeightProperty(float defaultValue) => new PropertyValue<float>("H", StyleChanged, defaultValue);
        protected PropertyValue<float> GetSpaceProperty(float defaultValue) => new PropertyValue<float>("S", StyleChanged, defaultValue);
        protected PropertyValue<float> GetOffsetBeforeProperty(float defaultValue) => new PropertyValue<float>("OB", StyleChanged, defaultValue);
        protected PropertyValue<float> GetOffsetAfterProperty(float defaultValue) => new PropertyValue<float>("OA", StyleChanged, defaultValue);
        protected PropertyValue<float> GetLineWidthProperty(float defaultValue) => new PropertyValue<float>("LW", StyleChanged, defaultValue);
        protected PropertyBoolValue GetParallelProperty(bool defaultValue) => new PropertyBoolValue("P", StyleChanged, defaultValue);
        protected PropertyValue<float> GetSquareSideProperty(float defaultValue) => new PropertyValue<float>("SS", StyleChanged, defaultValue);
        protected PropertyValue<int> GetLineCountProperty(int defaultValue) => new PropertyValue<int>("LC", StyleChanged, defaultValue);
        protected PropertyValue<float> GetAngleProperty(float defaultValue) => new PropertyValue<float>("A", StyleChanged, defaultValue);
        protected PropertyValue<float> GetStepProperty(float defaultValue) => new PropertyValue<float>("S", StyleChanged, defaultValue);
        protected PropertyValue<int> GetOutputProperty(int defaultValue) => new PropertyValue<int>("O", StyleChanged, defaultValue);
        protected PropertyValue<float> GetAngleBetweenProperty(float defaultValue) => new PropertyValue<float>("A", StyleChanged, defaultValue);
        protected PropertyEnumValue<ChevronFillerStyle.From> GetStartingFromProperty(ChevronFillerStyle.From defaultValue) => new PropertyEnumValue<ChevronFillerStyle.From>("SF", StyleChanged, defaultValue);

        public enum StyleType
        {
            ItemMask = 0xFF,
            GroupMask = ~ItemMask,

            [Description(nameof(Localize.LineStyle_RegularLinesGroup))]
            RegularLine = Markup.Item.RegularLine,

            [Description(nameof(Localize.LineStyle_Solid))]
            LineSolid,

            [Description(nameof(Localize.LineStyle_Dashed))]
            LineDashed,

            [Description(nameof(Localize.LineStyle_DoubleSolid))]
            LineDoubleSolid,

            [Description(nameof(Localize.LineStyle_DoubleDashed))]
            LineDoubleDashed,

            [Description(nameof(Localize.LineStyle_SolidAndDashed))]
            LineSolidAndDashed,

            [Description(nameof(Localize.LineStyle_SharkTeeth))]
            LineSharkTeeth,

            [Description(nameof(Localize.LineStyle_Empty))]
            [NotVisible]
            EmptyLine,


            [Description(nameof(Localize.LineStyle_StopLinesGroup))]
            StopLine = Markup.Item.StopLine,

            [Description(nameof(Localize.LineStyle_StopSolid))]
            StopLineSolid,

            [Description(nameof(Localize.LineStyle_StopDashed))]
            StopLineDashed,

            [Description(nameof(Localize.LineStyle_StopDouble))]
            StopLineDoubleSolid,

            [Description(nameof(Localize.LineStyle_StopDoubleDashed))]
            StopLineDoubleDashed,

            [Description(nameof(Localize.LineStyle_StopSolidAndDashed))]
            StopLineSolidAndDashed,

            [Description(nameof(Localize.LineStyle_StopSharkTeeth))]
            StopLineSharkTeeth,


            [Description(nameof(Localize.FillerStyle_Group))]
            Filler = Markup.Item.Filler,

            [Description(nameof(Localize.FillerStyle_Stripe))]
            FillerStripe,

            [Description(nameof(Localize.FillerStyle_Grid))]
            FillerGrid,

            [Description(nameof(Localize.FillerStyle_Solid))]
            FillerSolid,

            [Description(nameof(Localize.FillerStyle_Chevron))]
            FillerChevron,


            Filler3D = Filler | 0x80,

            [Description(nameof(Localize.FillerStyle_Pavement))]
            FillerPavement,

            [Description(nameof(Localize.FillerStyle_Grass))]
            FillerGrass,


            [Description(nameof(Localize.CrosswalkStyle_Group))]
            Crosswalk = Markup.Item.Crosswalk,

            [Description(nameof(Localize.CrosswalkStyle_Existent))]
            CrosswalkExistent,

            [Description(nameof(Localize.CrosswalkStyle_Zebra))]
            CrosswalkZebra,

            [Description(nameof(Localize.CrosswalkStyle_DoubleZebra))]
            CrosswalkDoubleZebra,

            [Description(nameof(Localize.CrosswalkStyle_ParallelSolidLines))]
            CrosswalkParallelSolidLines,

            [Description(nameof(Localize.CrosswalkStyle_ParallelDashedLines))]
            CrosswalkParallelDashedLines,

            [Description(nameof(Localize.CrosswalkStyle_Ladder))]
            CrosswalkLadder,

            [Description(nameof(Localize.CrosswalkStyle_Solid))]
            CrosswalkSolid,

            [Description(nameof(Localize.CrosswalkStyle_ChessBoard))]
            CrosswalkChessBoard,
        }
    }
}
