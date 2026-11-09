using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Demos.Carte.Controls;
using Demos.Domain.Map;

namespace Demos.Carte;

public sealed record HoverInfo(
    double WorldX,
    double WorldY,
    double Altitude,
    string FeatureName,
    bool IsInMap);

/// <summary>
/// Contrôle graphique interactif affichant la carte avec gestion du double-buffering,
/// zoom à la molette fluide centré sur le curseur, navigation panoramique (pan),
/// mini-carte (minimap) de repérage et outils de dessin interactifs (relief, sommets, fleuve, forêts, gisements).
/// </summary>
public sealed class MapCanvasControl : Control
{
    private VectorMapDefinition? _map;
    private Heightmap? _heightmap;
    private MapRenderStyle _style = MapRenderStyle.IgnPastel;

    // Navigation & Caméra
    private float _zoom = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private bool _isPanning;
    private Point _dragStartMouse;
    private PointF _dragStartPan;

    // Minimap
    private bool _showMinimap = true;
    private Rectangle _minimapRect;

    // Outils de dessin
    private EditorTool _currentTool = EditorTool.Navigate;
    private bool _isBrushing;
    private PointF _hoverWorldPt = PointF.Empty;
    private bool _isMouseInside;
    private bool _isDraggingCommune;
    private int _draggedCommuneIndex = -1;
    private PointF _dragCommuneStartWorld = PointF.Empty;
    private PointF _dragCommuneCurrentWorld = PointF.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double BrushRadius { get; set; } = 80.0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double BrushPower { get; set; } = 30.0; // Force d'élévation / dénivelé

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double BiomeBrushRadius { get; set; } = 50.0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string BiomeBrushType { get; set; } = "foret";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string BiomeBrushName { get; set; } = "Bois des Chênes";

    // Propriétés de rétrocompatibilité pour le pinceau forêt
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ForestBrushRadius { get => BiomeBrushRadius; set => BiomeBrushRadius = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ForestBrushName { get => BiomeBrushName; set => BiomeBrushName = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ResourceBrushRadius { get; set; } = 40.0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ResourceBrushType { get; set; } = "pierre";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ResourceBrushName { get; set; } = "Carrière de pierre";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double SmoothBrushRadius { get; set; } = 80.0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double SmoothBrushPower { get; set; } = 30.0;

    private double _flattenTargetAltitude = 0.0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ZoneMaskGrid? ZoneGrid { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceLabelCategory { get; set; } = "Peak";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceLabelName { get; set; } = "Mont Aiguille";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PlaceLabelShowAltitude { get; set; } = true;

    public List<Point2D> ActiveRiverPoints { get; } = [];
    public List<Point2D> SpontaneousPreviewRiver { get; private set; } = [];
    public bool IsDrawingRiver => _currentTool == EditorTool.DrawRiver;

    public List<Point2D> ActiveForestPoints { get; } = [];
    public bool IsDrawingForest => _currentTool == EditorTool.DrawForest;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EditorTool CurrentTool
    {
        get => _currentTool;
        set
        {
            _currentTool = value;
            Cursor = _currentTool == EditorTool.Navigate ? Cursors.Default : Cursors.Cross;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowContours { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowLabels { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowResources { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowCadastre { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowMinimap
    {
        get => _showMinimap;
        set { _showMinimap = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float ZoomLevel => _zoom;

    public event Action<HoverInfo>? HoverChanged;
    public event Action<float>? ZoomChanged;
    public event Action? ElevationModified;
    public event Action<double, double>? PlaceNamed;
    public event Action<double, double>? PlaceDeleted;
    public event Action<double, double>? PeakPlaced;
    public event Action<double, double>? RiverSourcePlaced;
    public event Action<double, double>? RiverDeleted;
    public event Action<int, double, double>? CommuneMoved;
    public event Action<double, double>? CommunePlaced;
    public event Action<double, double>? CommuneDeleted;
    public event Action? BiomeBrushApplied;
    public event Action<string>? BiomeValidationWarning;
    public event Action? ForestBrushApplied { add => BiomeBrushApplied += value; remove => BiomeBrushApplied -= value; }
    public event Action? ResourceBrushApplied;
    public event Action? FinishActionRequested;

    public MapCanvasControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.StandardDoubleClick,
            true);

        BackColor = Color.FromArgb(240, 235, 225);
    }

    public void SetMap(VectorMapDefinition map, Heightmap heightmap, MapRenderStyle style, ZoneMaskGrid? zoneGrid = null)
    {
        _map = map;
        _heightmap = heightmap;
        _style = style;
        if (zoneGrid is not null) ZoneGrid = zoneGrid;
        Invalidate();
    }

    public void SetStyle(MapRenderStyle style)
    {
        if (_style != style)
        {
            _style = style;
            Invalidate();
        }
    }

    public void ResetView()
    {
        _zoom = 1.0f;
        _panOffset = PointF.Empty;
        ZoomChanged?.Invoke(_zoom);
        Invalidate();
    }

    public void ZoomIn()
    {
        ApplyZoom(1.25f, new PointF(Width * 0.5f, Height * 0.5f));
    }

    public void ZoomOut()
    {
        ApplyZoom(1.0f / 1.25f, new PointF(Width * 0.5f, Height * 0.5f));
    }

    public void SetZoom(float zoom)
    {
        _zoom = Math.Clamp(zoom, 0.2f, 8.0f);
        ZoomChanged?.Invoke(_zoom);
        Invalidate();
    }

    public void ExportToPng(string filePath)
    {
        if (_map is null || _heightmap is null)
            return;

        var exportW = Math.Max(2000, (int)_map.Width * 2);
        var exportH = Math.Max(1280, (int)_map.Height * 2);

        using var bmp = new Bitmap(exportW, exportH);
        using (var g = Graphics.FromImage(bmp))
        {
            MapRenderer.Render(
                g,
                new Rectangle(0, 0, exportW, exportH),
                _map,
                _heightmap,
                _style,
                1.0f,
                PointF.Empty,
                ShowContours,
                ShowLabels,
                ShowResources,
                ShowCadastre);
        }

        bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_map is null || _heightmap is null)
        {
            using var font = new Font("Segoe UI", 12f);
            using var brush = new SolidBrush(Color.FromArgb(100, 90, 80));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("Génération de la carte en cours...", font, brush, ClientRectangle, sf);
            return;
        }

        // Rendu de la carte avec zoom et panoramique
        MapRenderer.Render(
            e.Graphics,
            ClientRectangle,
            _map,
            _heightmap,
            _style,
            _zoom,
            _panOffset,
            ShowContours,
            ShowLabels,
            ShowResources,
            ShowCadastre);

        // Dessin des surimpressions d'outils (Preview du pinceau, tracés en cours)
        DrawToolOverlays(e.Graphics);

        // Rendu de la mini-carte de repérage (si activée)
        if (_showMinimap)
        {
            DrawMinimap(e.Graphics);
        }
    }

    private void DrawToolOverlays(Graphics g)
    {
        if (_map is null) return;

        var baseScale = Math.Min((float)Width / (float)_map.Width, (float)Height / (float)_map.Height);
        var finalScale = baseScale * _zoom;

        var boundsCenter = new PointF(Width * 0.5f, Height * 0.5f);
        var mapCenterX = boundsCenter.X + _panOffset.X;
        var mapCenterY = boundsCenter.Y + _panOffset.Y;

        var mapLeft = mapCenterX - (float)_map.Width * finalScale * 0.5f;
        var mapTop = mapCenterY - (float)_map.Height * finalScale * 0.5f;

        var state = g.Save();
        g.TranslateTransform(mapLeft, mapTop);
        g.ScaleTransform(finalScale, finalScale);

        // 1. Surimpression du tracé spontané gravitaire du fleuve en cours
        if (_currentTool == EditorTool.DrawRiver && SpontaneousPreviewRiver.Count >= 2)
        {
            var pts = SpontaneousPreviewRiver.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();

            using var penGlow = new Pen(Color.FromArgb(90, 80, 200, 255), 8.0f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawCurve(penGlow, pts, 0.4f);

            using var penRiver = new Pen(Color.FromArgb(230, 20, 130, 210), 3.5f) { DashStyle = DashStyle.Dash, LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawCurve(penRiver, pts, 0.4f);

            var pSource = pts[0];
            using var sourceFill = new SolidBrush(Color.FromArgb(240, 20, 110, 220));
            using var sourcePen = new Pen(Color.White, 1.8f / finalScale);
            g.FillEllipse(sourceFill, pSource.X - 5, pSource.Y - 5, 10, 10);
            g.DrawEllipse(sourcePen, pSource.X - 5, pSource.Y - 5, 10, 10);

            var pMouth = pts[^1];
            using var mouthFill = new SolidBrush(Color.FromArgb(240, 0, 160, 180));
            g.FillEllipse(mouthFill, pMouth.X - 6, pMouth.Y - 6, 12, 12);
            g.DrawEllipse(sourcePen, pMouth.X - 6, pMouth.Y - 6, 12, 12);
        }
        else if (ActiveRiverPoints.Count > 0)
        {
            var pts = ActiveRiverPoints.Select(p => new PointF((float)p.X, (float)p.Y)).ToList();
            if (_isMouseInside) pts.Add(_hoverWorldPt);

            using var penRiverActive = new Pen(Color.FromArgb(220, 20, 140, 220), 4.0f) { DashStyle = DashStyle.Solid };
            g.DrawLines(penRiverActive, pts.ToArray());

            using var ptBrush = new SolidBrush(Color.FromArgb(240, 30, 80, 180));
            foreach (var p in ActiveRiverPoints)
            {
                g.FillEllipse(ptBrush, (float)p.X - 4, (float)p.Y - 4, 8, 8);
            }
        }

        // 2. Surimpression du massif forestier en cours
        if (ActiveForestPoints.Count > 0)
        {
            var pts = ActiveForestPoints.Select(p => new PointF((float)p.X, (float)p.Y)).ToList();
            if (_isMouseInside) pts.Add(_hoverWorldPt);

            using var forestPen = new Pen(Color.FromArgb(220, 60, 150, 60), 2.5f) { DashStyle = DashStyle.Dash };
            using var forestFill = new SolidBrush(Color.FromArgb(40, 80, 180, 80));

            if (pts.Count >= 3)
            {
                g.FillPolygon(forestFill, pts.ToArray());
            }
            if (pts.Count >= 2)
            {
                g.DrawLines(forestPen, pts.ToArray());
            }

            using var ptBrush = new SolidBrush(Color.FromArgb(240, 40, 120, 40));
            foreach (var p in ActiveForestPoints)
            {
                g.FillEllipse(ptBrush, (float)p.X - 3.5f, (float)p.Y - 3.5f, 7, 7);
            }
        }

        // 3. Réticules sous le curseur selon l'outil actif
        if (_isMouseInside)
        {
            switch (_currentTool)
            {
                case EditorTool.ElevateBrush:
                    var r = (float)BrushRadius;
                    var brushColor = Color.FromArgb(200, 34, 139, 34); // Vert forêt / relief
                    using (var brushPen = new Pen(brushColor, 2.0f / finalScale) { DashStyle = DashStyle.Dash })
                    using (var brushFill = new SolidBrush(Color.FromArgb(25, brushColor)))
                    {
                        g.FillEllipse(brushFill, _hoverWorldPt.X - r, _hoverWorldPt.Y - r, r * 2, r * 2);
                        g.DrawEllipse(brushPen, _hoverWorldPt.X - r, _hoverWorldPt.Y - r, r * 2, r * 2);
                    }
                    break;

                case EditorTool.SmoothBrush:
                    var rs = (float)SmoothBrushRadius;
                    var smoothColor = Color.FromArgb(210, 30, 140, 200); // Bleu azur / cyan lissage
                    using (var sPen = new Pen(smoothColor, 2.0f / finalScale) { DashStyle = DashStyle.Dash })
                    using (var sFill = new SolidBrush(Color.FromArgb(30, smoothColor)))
                    {
                        g.FillEllipse(sFill, _hoverWorldPt.X - rs, _hoverWorldPt.Y - rs, rs * 2, rs * 2);
                        g.DrawEllipse(sPen, _hoverWorldPt.X - rs, _hoverWorldPt.Y - rs, rs * 2, rs * 2);
                    }
                    break;

                case EditorTool.NamePlace:
                    var curAlt = _heightmap?.SampleElevation(_hoverWorldPt.X, _hoverWorldPt.Y) ?? 0.0;
                    var catObj = PlaceCategories.Get(PlaceLabelCategory);
                    var previewLabel = PlaceLabelShowAltitude
                        ? $"{catObj.Icon} {PlaceLabelName} ({curAlt:F0} m)"
                        : $"{catObj.Icon} {PlaceLabelName}";

                    using (var reticlePen = new Pen(Color.FromArgb(220, 160, 50, 30), 2.0f / finalScale))
                    using (var dashedPen = new Pen(Color.FromArgb(160, 160, 50, 30), 1.4f / finalScale) { DashStyle = DashStyle.Dash })
                    using (var fillCenter = new SolidBrush(Color.FromArgb(200, 180, 40, 30)))
                    {
                        g.DrawEllipse(dashedPen, _hoverWorldPt.X - 12, _hoverWorldPt.Y - 12, 24, 24);
                        g.DrawLine(reticlePen, _hoverWorldPt.X - 8, _hoverWorldPt.Y, _hoverWorldPt.X + 8, _hoverWorldPt.Y);
                        g.DrawLine(reticlePen, _hoverWorldPt.X, _hoverWorldPt.Y - 8, _hoverWorldPt.X, _hoverWorldPt.Y + 8);
                        g.FillEllipse(fillCenter, _hoverWorldPt.X - 3, _hoverWorldPt.Y - 3, 6, 6);
                    }

                    // Étiquette flottante prévisualisant le toponyme
                    using (var lblFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                    using (var bgTag = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                    using (var borderTag = new Pen(Color.FromArgb(180, 120, 40, 30), 1.2f / finalScale))
                    using (var textTag = new SolidBrush(Color.FromArgb(80, 40, 20)))
                    {
                        var tagSz = g.MeasureString(previewLabel, lblFont);
                        var tagX = _hoverWorldPt.X + 14;
                        var tagY = _hoverWorldPt.Y - 10;
                        g.FillRectangle(bgTag, tagX, tagY, tagSz.Width + 4, tagSz.Height);
                        g.DrawRectangle(borderTag, tagX, tagY, tagSz.Width + 4, tagSz.Height);
                        g.DrawString(previewLabel, lblFont, textTag, tagX + 2, tagY);
                    }
                    break;

                case EditorTool.DrawBiome:
                    var rb = (float)BiomeBrushRadius;
                    var bColor = BiomeBrushType.ToLowerInvariant() switch
                    {
                        "foret" or "forest" => Color.FromArgb(200, 46, 125, 50),
                        "fertile" or "zone fertile" => Color.FromArgb(200, 205, 155, 45),
                        "marais" => Color.FromArgb(200, 40, 140, 110),
                        "plage" => Color.FromArgb(200, 215, 175, 75),
                        "falaise" => Color.FromArgb(200, 110, 115, 125),
                        "desert" => Color.FromArgb(200, 215, 130, 40),
                        "glacier" => Color.FromArgb(200, 70, 160, 220),
                        _ => Color.FromArgb(200, 46, 125, 50)
                    };
                    using (var bPen = new Pen(bColor, 2.0f / finalScale) { DashStyle = DashStyle.Dash })
                    using (var bFill = new SolidBrush(Color.FromArgb(35, bColor)))
                    {
                        g.FillEllipse(bFill, _hoverWorldPt.X - rb, _hoverWorldPt.Y - rb, rb * 2, rb * 2);
                        g.DrawEllipse(bPen, _hoverWorldPt.X - rb, _hoverWorldPt.Y - rb, rb * 2, rb * 2);
                    }
                    break;

                case EditorTool.PlaceResource:
                    var rr = (float)ResourceBrushRadius;
                    var rColor = ResourceBrushType.ToLowerInvariant() switch
                    {
                        "charbon" => Color.FromArgb(200, 45, 45, 48),
                        "fer" => Color.FromArgb(200, 185, 95, 60),
                        _ => Color.FromArgb(200, 120, 145, 160) // pierre
                    };
                    using (var rPen = new Pen(rColor, 2.0f / finalScale) { DashStyle = DashStyle.Dash })
                    using (var rFill = new SolidBrush(Color.FromArgb(35, rColor)))
                    {
                        g.FillEllipse(rFill, _hoverWorldPt.X - rr, _hoverWorldPt.Y - rr, rr * 2, rr * 2);
                        g.DrawEllipse(rPen, _hoverWorldPt.X - rr, _hoverWorldPt.Y - rr, rr * 2, rr * 2);
                    }
                    break;

                case EditorTool.MoveCommune:
                    if (_isDraggingCommune && _draggedCommuneIndex >= 0 && _map is not null && _draggedCommuneIndex < _map.AllCommunes.Count)
                    {
                        var c = _map.AllCommunes[_draggedCommuneIndex];
                        var origX = (float)c.Position.X;
                        var origY = (float)c.Position.Y;
                        var curX = _dragCommuneCurrentWorld.X;
                        var curY = _dragCommuneCurrentWorld.Y;

                        using var linePen = new Pen(Color.FromArgb(200, 220, 60, 40), 1.5f / finalScale) { DashStyle = DashStyle.Dash };
                        g.DrawLine(linePen, origX, origY, curX, curY);

                        using var ghostBrush = new SolidBrush(Color.FromArgb(190, 220, 50, 40));
                        using var ghostPen = new Pen(Color.FromArgb(240, 255, 255, 255), 2.0f / finalScale);
                        g.FillEllipse(ghostBrush, curX - 8, curY - 8, 16, 16);
                        g.DrawEllipse(ghostPen, curX - 8, curY - 8, 16, 16);

                        using var font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                        using var textBrush = new SolidBrush(Color.FromArgb(220, 40, 20));
                        var labelText = _draggedCommuneIndex == 0 ? $"★ {c.Name} (Chef-lieu)" : c.Name;
                        g.DrawString(labelText, font, textBrush, curX, curY + 10);
                    }
                    else
                    {
                        var hoverIndex = FindCommuneAt(_hoverWorldPt.X, _hoverWorldPt.Y, 25.0);
                        if (hoverIndex >= 0 && _map is not null && hoverIndex < _map.AllCommunes.Count)
                        {
                            var c = _map.AllCommunes[hoverIndex];
                            using var ringPen = new Pen(Color.FromArgb(220, 220, 40, 30), 2.0f / finalScale) { DashStyle = DashStyle.Dot };
                            g.DrawEllipse(ringPen, (float)c.Position.X - 25, (float)c.Position.Y - 25, 50, 50);

                            using var font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                            using var tb = new SolidBrush(Color.FromArgb(180, 40, 20));
                            var roleText = hoverIndex == 0 ? "Glisser pour déplacer le chef-lieu" : "Glisser pour déplacer";
                            g.DrawString(roleText, font, tb, (float)c.Position.X, (float)c.Position.Y - 20);
                        }
                        else
                        {
                            using var comPen = new Pen(Color.FromArgb(220, 180, 30, 30), 2.0f / finalScale);
                            using var comBrush = new SolidBrush(Color.FromArgb(160, 200, 40, 40));
                            g.FillEllipse(comBrush, _hoverWorldPt.X - 6, _hoverWorldPt.Y - 6, 12, 12);
                            g.DrawEllipse(comPen, _hoverWorldPt.X - 12, _hoverWorldPt.Y - 12, 24, 24);
                        }
                    }
                    break;
            }
        }

        g.Restore(state);

        // 4. Badge HUD contextuel au sommet du canvas (mode dessin fleuve/forêt)
        CanvasHudOverlay.Draw(
            g,
            _currentTool,
            BiomeBrushType,
            ResourceBrushType,
            _map?.AllRivers.Count ?? 0,
            _map?.AllCommunes.Count ?? 0);
    }

    private void DrawMinimap(Graphics g)
    {
        if (_map is null || _heightmap is null)
            return;

        _minimapRect = CanvasMinimapRenderer.Render(g, Width, Height, _map, GetVisibleWorldRect());
    }

    private RectangleF GetVisibleWorldRect()
    {
        if (_map is null) return RectangleF.Empty;

        var baseScale = Math.Min((float)Width / (float)_map.Width, (float)Height / (float)_map.Height);
        var finalScale = baseScale * _zoom;

        var boundsCenter = new PointF(Width * 0.5f, Height * 0.5f);
        var mapCenterX = boundsCenter.X + _panOffset.X;
        var mapCenterY = boundsCenter.Y + _panOffset.Y;

        var mapLeft = mapCenterX - (float)_map.Width * finalScale * 0.5f;
        var mapTop = mapCenterY - (float)_map.Height * finalScale * 0.5f;

        var wx0 = (0 - mapLeft) / finalScale;
        var wy0 = (0 - mapTop) / finalScale;
        var wx1 = (Width - mapLeft) / finalScale;
        var wy1 = (Height - mapTop) / finalScale;

        return new RectangleF(wx0, wy0, wx1 - wx0, wy1 - wy0);
    }

    private PointF ScreenToWorld(Point screenPt)
    {
        if (_map is null) return PointF.Empty;

        var baseScale = Math.Min((float)Width / (float)_map.Width, (float)Height / (float)_map.Height);
        var finalScale = baseScale * _zoom;

        var boundsCenter = new PointF(Width * 0.5f, Height * 0.5f);
        var mapCenterX = boundsCenter.X + _panOffset.X;
        var mapCenterY = boundsCenter.Y + _panOffset.Y;

        var mapLeft = mapCenterX - (float)_map.Width * finalScale * 0.5f;
        var mapTop = mapCenterY - (float)_map.Height * finalScale * 0.5f;

        var wx = (screenPt.X - mapLeft) / finalScale;
        var wy = (screenPt.Y - mapTop) / finalScale;

        return new PointF(wx, wy);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        var factor = e.Delta > 0 ? 1.18f : (1.0f / 1.18f);
        ApplyZoom(factor, e.Location);
    }

    private void ApplyZoom(float factor, PointF pivot)
    {
        var oldZoom = _zoom;
        var newZoom = Math.Clamp(_zoom * factor, 0.2f, 8.0f);
        if (Math.Abs(newZoom - oldZoom) < 0.001f)
            return;

        var boundsCenter = new PointF(Width * 0.5f, Height * 0.5f);
        var relX = pivot.X - boundsCenter.X;
        var relY = pivot.Y - boundsCenter.Y;

        // Zoom centré sur le curseur de souris
        _panOffset = new PointF(
            relX - (relX - _panOffset.X) * (newZoom / oldZoom),
            relY - (relY - _panOffset.Y) * (newZoom / oldZoom));

        _zoom = newZoom;
        ZoomChanged?.Invoke(_zoom);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        // Clic sur la minimap pour téléporter la vue
        if (_showMinimap && _minimapRect.Contains(e.Location) && _map is not null)
        {
            var miniScale = (float)_minimapRect.Width / (float)_map.Width;
            var targetWorldX = (e.X - _minimapRect.X) / miniScale;
            var targetWorldY = (e.Y - _minimapRect.Y) / miniScale;

            CenterOnWorld(new PointF(targetWorldX, targetWorldY));
            return;
        }

        // Clic droit ou clic molette pour lancer le pan (sauf en mode Pinceau Terrain, Nommer un lieu, Fleuve, Commune, Biome ou Gisement)
        if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Right && _currentTool != EditorTool.ElevateBrush && _currentTool != EditorTool.NamePlace && _currentTool != EditorTool.DrawRiver && _currentTool != EditorTool.MoveCommune && _currentTool != EditorTool.DrawBiome && _currentTool != EditorTool.PlaceResource && _currentTool != EditorTool.SmoothBrush))
        {
            _isPanning = true;
            _dragStartMouse = e.Location;
            _dragStartPan = _panOffset;
            Cursor = Cursors.SizeAll;
            return;
        }

        // Clic gauche ou clic droit : exécution de l'outil actif
        if (_map is not null && _heightmap is not null)
        {
            var worldPt = ScreenToWorld(e.Location);
            var wx = worldPt.X;
            var wy = worldPt.Y;

            if (wx < 0 || wx > _map.Width || wy < 0 || wy > _map.Height)
                return;

            if (_currentTool == EditorTool.ElevateBrush && (e.Button is MouseButtons.Left or MouseButtons.Right))
            {
                _isBrushing = true;
                var delta = e.Button == MouseButtons.Left ? Math.Abs(BrushPower) : -Math.Abs(BrushPower);
                _heightmap.ApplyBrush(wx, wy, BrushRadius, delta);
                var updatedWater = ContourExtractor.ExtractWaterBodies(_heightmap);
                _map = _map with { WaterBodies = updatedWater };
                Invalidate();
                return;
            }

            if (_currentTool == EditorTool.SmoothBrush && (e.Button is MouseButtons.Left or MouseButtons.Right))
            {
                _isBrushing = true;
                var powerFactor = Math.Clamp(SmoothBrushPower / 100.0, 0.05, 1.0);
                if (e.Button == MouseButtons.Right)
                {
                    _flattenTargetAltitude = _heightmap.SampleElevation(wx, wy);
                    _heightmap.ApplyFlattenBrush(wx, wy, SmoothBrushRadius, _flattenTargetAltitude, powerFactor);
                }
                else
                {
                    _heightmap.ApplySmoothBrush(wx, wy, SmoothBrushRadius, powerFactor);
                }
                var updatedWater = ContourExtractor.ExtractWaterBodies(_heightmap);
                _map = _map with { WaterBodies = updatedWater };
                Invalidate();
                return;
            }

            if (_currentTool == EditorTool.DrawBiome && (e.Button is MouseButtons.Left or MouseButtons.Right))
            {
                _isBrushing = true;
                var isAdding = e.Button == MouseButtons.Left;
                string? warn = null;
                var modified = ZoneGrid?.ApplyBiomeBrush(wx, wy, BiomeBrushRadius, BiomeBrushType, isAdding, _heightmap, _map.AllRivers, out warn) ?? false;
                if (warn != null)
                {
                    BiomeValidationWarning?.Invoke(warn);
                }
                var biomes = ZoneGrid?.ExtractBiomes(BiomeBrushType, BiomeBrushName) ?? [];
                var forests = ZoneGrid?.ExtractForests(BiomeBrushName) ?? [];
                _map = _map with { Biomes = biomes, Forests = forests };
                Invalidate();
                return;
            }

            if (_currentTool == EditorTool.PlaceResource && (e.Button is MouseButtons.Left or MouseButtons.Right))
            {
                _isBrushing = true;
                ZoneGrid?.ApplyResourceBrush(wx, wy, ResourceBrushRadius, ResourceBrushType, isAdding: e.Button == MouseButtons.Left);
                var (zones, deposits) = ZoneGrid?.ExtractResources(ResourceBrushName) ?? ([], []);
                _map = _map with { ResourceZones = zones, Resources = deposits };
                Invalidate();
                return;
            }

            if (_currentTool == EditorTool.NamePlace)
            {
                if (e.Button == MouseButtons.Left)
                {
                    PlaceNamed?.Invoke(wx, wy);
                    PeakPlaced?.Invoke(wx, wy);
                    Invalidate();
                    return;
                }
                if (e.Button == MouseButtons.Right)
                {
                    PlaceDeleted?.Invoke(wx, wy);
                    Invalidate();
                    return;
                }
            }

            if (_currentTool == EditorTool.DrawRiver)
            {
                if (e.Button == MouseButtons.Left)
                {
                    RiverSourcePlaced?.Invoke(wx, wy);
                    Invalidate();
                    return;
                }
                if (e.Button == MouseButtons.Right)
                {
                    RiverDeleted?.Invoke(wx, wy);
                    Invalidate();
                    return;
                }
            }

            if (_currentTool == EditorTool.MoveCommune)
            {
                if (e.Button == MouseButtons.Left)
                {
                    var clickedIndex = FindCommuneAt(wx, wy, 25.0);
                    if (clickedIndex >= 0)
                    {
                        _isDraggingCommune = true;
                        _draggedCommuneIndex = clickedIndex;
                        _dragCommuneStartWorld = new PointF((float)wx, (float)wy);
                        _dragCommuneCurrentWorld = new PointF((float)wx, (float)wy);
                        Cursor = Cursors.SizeAll;
                        Invalidate();
                        return;
                    }

                    CommunePlaced?.Invoke(wx, wy);
                    Invalidate();
                    return;
                }
                if (e.Button == MouseButtons.Right)
                {
                    CommuneDeleted?.Invoke(wx, wy);
                    Invalidate();
                    return;
                }
            }
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (_currentTool == EditorTool.DrawRiver && ActiveRiverPoints.Count >= 2)
        {
            FinishActionRequested?.Invoke();
        }
        else if (_currentTool == EditorTool.DrawForest && ActiveForestPoints.Count >= 3)
        {
            FinishActionRequested?.Invoke();
        }
    }

    public void CenterOnWorld(PointF worldPt)
    {
        if (_map is null) return;

        var baseScale = Math.Min((float)Width / (float)_map.Width, (float)Height / (float)_map.Height);
        var finalScale = baseScale * _zoom;

        var worldCenterX = (float)_map.Width * 0.5f;
        var worldCenterY = (float)_map.Height * 0.5f;

        _panOffset = new PointF(
            (worldCenterX - worldPt.X) * finalScale,
            (worldCenterY - worldPt.Y) * finalScale);

        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            _panOffset = new PointF(
                _dragStartPan.X + (e.X - _dragStartMouse.X),
                _dragStartPan.Y + (e.Y - _dragStartMouse.Y));
            Invalidate();
            return;
        }

        if (_map is not { } map || _heightmap is not { } heightmap)
            return;

        var worldPt = ScreenToWorld(e.Location);
        _hoverWorldPt = worldPt;
        _isMouseInside = true;

        var wx = worldPt.X;
        var wy = worldPt.Y;

        if (_isDraggingCommune && _draggedCommuneIndex >= 0)
        {
            _dragCommuneCurrentWorld = new PointF((float)wx, (float)wy);
            Invalidate();
            return;
        }

        if (_currentTool == EditorTool.MoveCommune && !_isDraggingCommune)
        {
            var hoverIndex = FindCommuneAt(wx, wy, 25.0);
            Cursor = hoverIndex >= 0 ? Cursors.Hand : Cursors.Cross;
        }

        // Sécurité : si aucun bouton souris n'est pressé, couper immédiatement le brossage
        if ((e.Button & (MouseButtons.Left | MouseButtons.Right)) == 0)
        {
            if (_isBrushing)
            {
                EndBrushing();
            }
        }

        // Pinceau continu (glissé) uniquement si un bouton souris est activement maintenu
        if (_isBrushing && (e.Button & (MouseButtons.Left | MouseButtons.Right)) != 0)
        {
            if (_currentTool == EditorTool.ElevateBrush)
            {
                var delta = (e.Button & MouseButtons.Left) != 0 ? Math.Abs(BrushPower) : -Math.Abs(BrushPower);
                heightmap.ApplyBrush(wx, wy, BrushRadius, delta * 0.4);
                var updatedWater = ContourExtractor.ExtractWaterBodies(heightmap);
                map = map with { WaterBodies = updatedWater };
                _map = map;
                Invalidate();
            }
            else if (_currentTool == EditorTool.SmoothBrush)
            {
                var powerFactor = Math.Clamp(SmoothBrushPower / 100.0, 0.05, 1.0);
                if ((e.Button & MouseButtons.Right) != 0)
                {
                    heightmap.ApplyFlattenBrush(wx, wy, SmoothBrushRadius, _flattenTargetAltitude, powerFactor * 0.4);
                }
                else
                {
                    heightmap.ApplySmoothBrush(wx, wy, SmoothBrushRadius, powerFactor * 0.4);
                }
                var updatedWater = ContourExtractor.ExtractWaterBodies(heightmap);
                map = map with { WaterBodies = updatedWater };
                _map = map;
                Invalidate();
            }
            else if (_currentTool == EditorTool.DrawBiome)
            {
                var isAdding = (e.Button & MouseButtons.Left) != 0;
                string? warn = null;
                var modified = ZoneGrid?.ApplyBiomeBrush(wx, wy, BiomeBrushRadius, BiomeBrushType, isAdding, heightmap, map.AllRivers, out warn) ?? false;
                if (warn != null)
                {
                    BiomeValidationWarning?.Invoke(warn);
                }
                var biomes = ZoneGrid?.ExtractBiomes(BiomeBrushType, BiomeBrushName) ?? [];
                var forests = ZoneGrid?.ExtractForests(BiomeBrushName) ?? [];
                map = map with { Biomes = biomes, Forests = forests };
                _map = map;
                Invalidate();
            }
            else if (_currentTool == EditorTool.PlaceResource)
            {
                var isAdding = (e.Button & MouseButtons.Left) != 0;
                ZoneGrid?.ApplyResourceBrush(wx, wy, ResourceBrushRadius, ResourceBrushType, isAdding);
                var (zones, deposits) = ZoneGrid?.ExtractResources(ResourceBrushName) ?? ([], []);
                map = map with { ResourceZones = zones, Resources = deposits };
                _map = map;
                Invalidate();
            }
        }

        // Prévisualisation en direct du fleuve gravitaire spontané
        if (_currentTool == EditorTool.DrawRiver && wx >= 0 && wx <= map.Width && wy >= 0 && wy <= map.Height)
        {
            SpontaneousPreviewRiver = heightmap.SimulateSpontaneousRiver(wx, wy, map.AllRivers);
            Invalidate();
        }

        if (wx >= 0 && wx <= map.Width && wy >= 0 && wy <= map.Height)
        {
            var alt = heightmap.SampleElevation(wx, wy);
            var feature = DetectFeature(wx, wy, alt, map);
            HoverChanged?.Invoke(new HoverInfo(Math.Round(wx, 1), Math.Round(wy, 1), alt, feature, true));
        }
        else
        {
            HoverChanged?.Invoke(new HoverInfo(0, 0, 0, "Hors limites", false));
        }

        // Redessiner pour mettre à jour la prévisualisation du réticule de l'outil
        if (_currentTool != EditorTool.Navigate)
        {
            Invalidate();
        }
    }

    private void EndBrushing()
    {
        if (!_isBrushing) return;
        _isBrushing = false;

        if (_currentTool == EditorTool.ElevateBrush)
        {
            if (_heightmap is not null && _map is not null)
            {
                var updatedWater = ContourExtractor.ExtractWaterBodies(_heightmap);
                _map = _map with { WaterBodies = updatedWater };
            }
            ElevationModified?.Invoke();
        }
        else if (_currentTool == EditorTool.SmoothBrush)
        {
            if (_heightmap is not null && _map is not null)
            {
                var updatedWater = ContourExtractor.ExtractWaterBodies(_heightmap);
                _map = _map with { WaterBodies = updatedWater };
            }
            ElevationModified?.Invoke();
        }
        else if (_currentTool == EditorTool.DrawBiome)
        {
            BiomeBrushApplied?.Invoke();
        }
        else if (_currentTool == EditorTool.PlaceResource)
        {
            ResourceBrushApplied?.Invoke();
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = _currentTool == EditorTool.Navigate ? Cursors.Default : Cursors.Cross;
        }

        if (_isDraggingCommune)
        {
            _isDraggingCommune = false;
            var index = _draggedCommuneIndex;
            _draggedCommuneIndex = -1;
            Cursor = _currentTool == EditorTool.MoveCommune ? Cursors.Hand : Cursors.Default;

            var dx = _dragCommuneCurrentWorld.X - _dragCommuneStartWorld.X;
            var dy = _dragCommuneCurrentWorld.Y - _dragCommuneStartWorld.Y;
            var distMoved = Math.Sqrt(dx * dx + dy * dy);

            if (distMoved >= 2.0 && index >= 0)
            {
                CommuneMoved?.Invoke(index, _dragCommuneCurrentWorld.X, _dragCommuneCurrentWorld.Y);
            }
            Invalidate();
        }

        EndBrushing();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isMouseInside = true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_isBrushing)
        {
            EndBrushing();
        }
        if (_isDraggingCommune)
        {
            _isDraggingCommune = false;
            _draggedCommuneIndex = -1;
        }
        _isPanning = false;
        _isMouseInside = false;
        HoverChanged?.Invoke(new HoverInfo(0, 0, 0, "", false));
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        if (_isBrushing)
        {
            EndBrushing();
        }
        _isPanning = false;
    }

    public bool TestBrushHoverSafety()
    {
        _isBrushing = true;
        _currentTool = EditorTool.ElevateBrush;
        var initialAlt = _heightmap?.SampleElevation(100, 100) ?? 0;
        OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 100, 100, 0));
        return !_isBrushing;
    }

    public int FindCommuneAt(double wx, double wy, double maxDist)
    {
        if (_map is null) return -1;
        var communes = _map.AllCommunes;
        var bestIndex = -1;
        var bestDist = maxDist;
        for (var i = 0; i < communes.Count; i++)
        {
            var c = communes[i];
            var d = Math.Sqrt(Math.Pow(c.Position.X - wx, 2) + Math.Pow(c.Position.Y - wy, 2));
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static string DetectFeature(double wx, double wy, double alt, VectorMapDefinition map)
    {
        // 1. Communes
        foreach (var c in map.AllCommunes)
        {
            var d = Math.Sqrt(Math.Pow(wx - c.Position.X, 2) + Math.Pow(wy - c.Position.Y, 2));
            if (d < 22.0)
            {
                var role = c == map.DefaultCommune ? " (Chef-lieu initial)" : "";
                return $"Commune : {c.Name}{role}";
            }
        }

        // 2. Lieux nommés & toponymie
        foreach (var spot in map.SpotElevations)
        {
            var d = Math.Sqrt(Math.Pow(wx - spot.Position.X, 2) + Math.Pow(wy - spot.Position.Y, 2));
            if (d < 22.0)
            {
                var cat = PlaceCategories.Get(spot.Category);
                return spot.ShowAltitude
                    ? $"{spot.Name} ({cat.DisplayName} · Alt. {spot.Altitude:F0} m)"
                    : $"{spot.Name} ({cat.DisplayName})";
            }
        }

        // 3. Gisements & Zones de ressources
        foreach (var zone in map.AllResourceZones)
        {
            if (IsPointInPolygon(wx, wy, zone.Boundary) && !(zone.Holes?.Any(h => IsPointInPolygon(wx, wy, h)) ?? false))
            {
                var icon = zone.Kind switch { "pierre" => "🪨", "charbon" => "⛏️", "fer" => "⛓️", "fertile" => "🌾", _ => "💎" };
                return $"{icon} {zone.Name} ({zone.Kind})";
            }
        }
        foreach (var res in map.Resources)
        {
            var d = Math.Sqrt(Math.Pow(wx - res.Position.X, 2) + Math.Pow(wy - res.Position.Y, 2));
            if (d < 18.0)
                return $"Gisement de {res.Resource} ({res.Region})";
        }

        // 4. Fleuves & cours d'eau
        foreach (var river in map.AllRivers)
        {
            if (river.Points.Count >= 2)
            {
                for (var i = 0; i < river.Points.Count - 1; i++)
                {
                    var p1 = river.Points[i];
                    var p2 = river.Points[i + 1];
                    if (DistancePointToSegment(wx, wy, p1.X, p1.Y, p2.X, p2.Y) < 14.0)
                        return $"Cours d'eau \"{river.Name}\"";
                }
            }
        }

        // 5. Biomes naturels
        foreach (var biome in map.AllBiomes)
        {
            if (IsPointInPolygon(wx, wy, biome.Boundary) && !(biome.Holes?.Any(h => IsPointInPolygon(wx, wy, h)) ?? false))
            {
                var category = biome.Kind.ToLowerInvariant() switch
                {
                    "foret" or "forest" => "Massif forestier",
                    "fertile" or "zone fertile" => "Terre fertile",
                    "marais" => "Zone humide & marais",
                    "plage" => "Plage & littoral sableux",
                    "falaise" => "Falaises abruptes",
                    "desert" => "Désert & dunes",
                    "glacier" => "Glacier éternel",
                    _ => "Milieu naturel"
                };
                return $"{biome.Name} ({category})";
            }
        }

        // 6. Mer / Baie
        foreach (var water in map.WaterBodies)
        {
            if (IsPointInPolygon(wx, wy, water.Boundary))
                return $"{water.Name} (Étendue maritime)";
        }

        // 7. Terrains selon altitude
        if (alt >= 400) return "Massif rocheux du Nord (Hautes terres)";
        if (alt >= 200) return "Piémonts et collines boisées";
        if (alt >= 60) return "Plaine alluviale arable";
        return "Basse vallée et terres côtières";
    }

    private static bool IsPointInPolygon(double x, double y, System.Collections.Generic.IReadOnlyList<Point2D> poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Y > y) != (poly[j].Y > y)) &&
                (x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 0.0001)
            return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

        var t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
        var projX = x1 + t * dx;
        var projY = y1 + t * dy;
        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }
}
