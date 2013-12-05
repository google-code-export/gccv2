using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Diagnostics;

namespace GpsCycleComputer
{
    class Graph
    {
        public Form1 parent;

        int[] x;
        int[] x2;
        int xScaleP = 1, xScaleQ = 1;
        int xUnit = 1;
        string xLabel1, xLabel2;
        short[] y;
        short[] y2;
        int yScaleP = 1, yScaleQ = 1;
        string title1, title2;

        int xMax = 0;   //Graph scale in user values (as displayed)
        int xMin = 0;
        int xDiv = 1;
        int yMax = 0;
        int yMin = 0;
        int yDiv = 1;
        long xFactor;    //factor to convert arrayValue to screenPixel
        int yFactor;
        int xMinOrg, xMaxOrg;           //xMin in original units
        int yMinOrg, yMaxOrg;
        int xSector;            //xMax-xMin in user units
        int ySector;

        int Index2draw = -1;       // -1: draw grid and t2f             else: draw track from index

        public bool alignT2f = false;
        public bool hideTrack = false;
        public bool hideT2f = false;

        public enum SourceX
        { Time, Distance, Old }
        public SourceX sourceX = SourceX.Distance;
        public enum SourceY
        { Alt, Speed, Heart, Old }
        public SourceY sourceY = SourceY.Alt;

        public enum DrawMode
        {
            Draw,
            Track,
            KeepAutoscale
        } public DrawMode drawMode = DrawMode.KeepAutoscale;

        public enum ScaleCmd
        {
            Nothing,
            DoAutoscale,
            DoYAutoscale,
            DoAutoscaleNoUndo,
            DoRedraw
        } public ScaleCmd scaleCmd = ScaleCmd.DoAutoscale;

        public enum MoveState
        {
            Nothing,
            MoveStart,
            Move,
            MoveEnd
        } public MoveState moveState = MoveState.Nothing;

        public enum MousePos : byte { none, top, bottom, left, right, middle };
        public MousePos mousePos = MousePos.none;


        public void DrawSourceNext()
        {
            SourceY s = sourceY;
            for (int i = 0; i < 3; i++)
            {
                s++;
                if (s > SourceY.Heart)
                    s = SourceY.Alt;
                if (parent.mPage.mBAr[(int)MenuPage.BFkt.graph_alt + (int)s].enabled)
                    break;
            }
            SetSource(s, SourceX.Old);
        }
        public void SetSource(SourceY srcY, SourceX srcX)
        {
            if (srcY != SourceY.Old && srcY != sourceY)
            {
                sourceY = srcY;
                scaleCmd = ScaleCmd.DoAutoscaleNoUndo;
            }
            if (sourceY == SourceY.Speed)
            {
                y = parent.PlotS;
                y2 = null;
                title1 = "Speed [/";
                if ((parent.comboUnits.SelectedIndex == 0) || (parent.comboUnits.SelectedIndex == 3))
                {
                    yScaleP = 32; yScaleQ = 515;
                    title2 = "mph]";
                }
                else
                {
                    yScaleP = 1; yScaleQ = 10;
                    title2 = "km/h]";
                }
            }
            else if (sourceY == SourceY.Heart)
            {
                y = parent.PlotH;
                y2 = null;
                yScaleP = 1; yScaleQ = 1;
                title1 = "Heart Rate [/"; title2 = "bpm]";
            }
            else  //if (sourceY == SourceY.Alt)
            {
                y = parent.PlotZ;
                y2 = parent.Plot2ndZ;
                title1 = "Altitude [/";
                if ((parent.comboUnits.SelectedIndex == 3) || (parent.comboUnits.SelectedIndex == 5) || (parent.comboUnits.SelectedIndex == 6))
                {
                    yScaleP = 1250; yScaleQ = 381;    // altitude in feet
                    title2 = "feet]";
                }
                else
                {
                    yScaleP = 1; yScaleQ = 1;
                    title2 = "m]";
                }
            }

            //int indexMin = -1;
            //int indexMax = -1;
            if (srcX != SourceX.Old && srcX != sourceX)
            {
                sourceX = srcX;
                //if (autostate != Autostate.DoAutoscale)       //todo keep x section
                //{


                //    for (int i = 0; i < parent.PlotCount; i++)
                //    {
                //        if (indexMin == -1 && x[i] >= xMin) indexMin = i;
                //        if (x[i] <= xMax) indexMax = i;
                //    }
                //}
                scaleCmd = ScaleCmd.DoAutoscaleNoUndo;
            }
            if (sourceX == SourceX.Distance)
            {
                x = parent.PlotD;
                x2 = parent.Plot2ndD;
                xLabel1 = "Distance [/"; //xLabel2 = "m]";
                if ((parent.comboUnits.SelectedIndex == 0) || (parent.comboUnits.SelectedIndex == 3))
                {
                    xScaleP = 64; xScaleQ = 103;
                    xLabel2 = "miles]";
                }
                else
                {
                    xScaleP = 1; xScaleQ = 1;   //additional settings in UpdateScaling()
                }
            }
            else  //if (sourceX == SourceX.Time)
            {
                x = parent.PlotT;
                x2 = parent.Plot2ndT;
                xScaleP = 1; xScaleQ = 1;
                //xUnit = 1;
                xLabel1 = "Time [/"; //xLabel2 = "sec]";
            }
            UpdateScaling(false);       //also ensures that whole graph is drawn (Index2draw=-1)
        }

        public void GraphZoomIn()
        {
            int mid2 = xMax + xMin;
            xMin = (2 * xMin + mid2) / 4;
            xMax = (2 * xMax + mid2) / 4;
            if (drawMode == DrawMode.KeepAutoscale)
                drawMode = DrawMode.Track;
            UpdateScaling(false);
            parent.NoBkPanel.Invalidate();
        }
        public void GraphZoomOut()
        {
            int mid2 = xMax + xMin;
            xMin = (4 * xMin - mid2) / 2;
            xMax = (4 * xMax - mid2) / 2;
            if (drawMode == DrawMode.KeepAutoscale)
                drawMode = DrawMode.Track;
            UpdateScaling(false);
            parent.NoBkPanel.Invalidate();
        }

        private void Autoscale(bool full)    //full or only y autoscale
        {
            xMinUndo = xMin; xMaxUndo = xMax; xUnitUndo = xUnit;
            yMinUndo = yMin; yMaxUndo = yMax;
            if (scaleCmd == ScaleCmd.DoAutoscaleNoUndo)
            {
                undoPossible = false;
                x2Ofs = 0;
            }
            else
                undoPossible = true;
            scaleCmd = ScaleCmd.Nothing;
            if (drawMode == DrawMode.Draw && (parent.GpsDataState == Form1.GpsOk || parent.oHeartBeat != null))
            {
                drawMode = DrawMode.Track;        //first autoscale: only switch to Track Mode
                Index2draw = -1;
                return;
            }

            int PlotCountCurrent = Math.Max(parent.PlotCount - 1, parent.CurrentPlotIndex);
            int Plot2ndCount = parent.Plot2ndCount;

            if (!full && xMax == short.MinValue && xMin == short.MaxValue)
                full = true;        //if coming from speed without data
            if (full)
            {
                xMax = short.MinValue;
                xMin = short.MaxValue;
            }
            yMax = short.MinValue;
            yMin = short.MaxValue;
            if (PlotCountCurrent >= 0 && !hideTrack)
            {
                if (full)
                {
                    xMin = x[0];
                    xMax = x[PlotCountCurrent];
                }
                for (int i = 0; i <= PlotCountCurrent; i++)
                {
                    //if (x[i] < xMin) continue;
                    //if (x[i] > xMax) break;
                    if (y[i] == Int16.MinValue) continue;       //ignore invalid values
                    if (y[i] > yMax) yMax = y[i];
                    if (y[i] < yMin) yMin = y[i];
                }
            }
            if (y2 != null && Plot2ndCount > 0 && !hideT2f)
            {
                if (full)
                {
                    if (x2[0] + x2Ofs < xMin) xMin = x2[0] + x2Ofs;
                    if (x2[Plot2ndCount - 1] + x2Ofs > xMax) xMax = x2[Plot2ndCount - 1] + x2Ofs;
                }
                for (int i = 0; i < Plot2ndCount; i++)
                {
                    //if (x2[i] < xMin) continue;       //only evaluate visible points
                    //if (x2[i] > xMax) break;
                    if (y2[i] == Int16.MinValue) continue;       //ignore invalid values
                    if (y2[i] > yMax) yMax = y2[i];
                    if (y2[i] < yMin) yMin = y2[i];
                }
            }
            if (full)
            {
                xUnit = 1;
                int xMaxPrev = xMax;                 //change to user units
                xMax = xMax * xScaleP / xScaleQ;
                if (xMax * xScaleQ / xScaleP != xMaxPrev) xMax++;
                xMin = xMin * xScaleP / xScaleQ;

                drawMode = DrawMode.KeepAutoscale;
                x2OfsDrawn = x2Ofs;
            }

            int yMaxPrev = yMax;
            yMax = yMax * yScaleP / yScaleQ;
            if (yMax * yScaleQ / yScaleP != yMaxPrev) yMax++;
            yMin = yMin * yScaleP / yScaleQ;
            UpdateScaling(false);
        }

        void UpdateScaling(bool shift)      //when shifting keep zoom fix (otherwise it would change slightly in most cases)
        {
            int limit = Int32.MaxValue / 20 / xScaleQ * xScaleP / xUnit;      //check limits (Scale maximal 20)
            if (xMax > limit) xMax = limit;
            if (xMin < -limit) xMin = -limit;
            limit = Int16.MaxValue * yScaleP / yScaleQ;
            if (yMax > limit) yMax = limit;
            if (yMin < -limit) yMin = -limit;

            int xMaxU = xMax * xUnit;

            if (sourceX == SourceX.Distance)
            {
                if (xScaleP == 1)
                {
                    if ((xMax - xMin) * xUnit > 3000)
                    {
                        xMax = xMaxU / 1000;
                        if (xMax * 1000 != xMaxU) xMax++;
                        xMin = xMin * xUnit / 1000;
                        xUnit = 1000;
                        xLabel2 = "km]";
                    }
                    else
                    {
                        xMax = xMaxU;
                        xMin = xMin * xUnit;
                        xUnit = 1;
                        xLabel2 = "m]";
                    }
                }
                else
                {
                    xMax = xMaxU / 1000;            //miles
                    if (xMax * 1000 != xMaxU) xMax++;
                    xMin = xMin * xUnit / 1000;
                    xUnit = 1000;
                }
            }
            else if (sourceX == SourceX.Time)   //Time
            {
                if ((xMax - xMin) * xUnit > 10800)        //3h
                {
                    xMax = xMaxU / 3600;
                    if (xMax * 3600 != xMaxU) xMax++;
                    xMin = xMin * xUnit / 3600;
                    xUnit = 3600;
                    xLabel2 = "h]";
                }
                else if ((xMax - xMin) * xUnit > 180)     //3min
                {
                    xMax = xMaxU / 60;
                    if (xMax * 60 != xMaxU) xMax++;
                    xMin = xMin * xUnit / 60;
                    xUnit = 60;
                    xLabel2 = "min]";
                }
                else
                {
                    xMax = xMaxU;
                    xMin = xMin * xUnit;
                    xUnit = 1;
                    xLabel2 = "sec]";
                }
            }
            if (shift)
            {
                int dummy = xMin;
                RoundMinMax(ref xMin, ref xMax);
                xMin = xMax - xSector;              //shift never called at first
                dummy = yMin;
                RoundMinMax(ref yMin, ref yMax);
                yMin = yMax - ySector;
            }
            else
            {
                xDiv = RoundMinMax(ref xMin, ref xMax);
                yDiv = RoundMinMax(ref yMin, ref yMax);
            }
            xFactor = parent.NoBkPanel.Width * xScaleP * 11 / xScaleQ / 12;    //w-20     30037  91.6%
            yFactor = parent.NoBkPanel.Height * yScaleP * 9 / yScaleQ / 10;   //h-26    29414   90%
            xMinOrg = xMin * xScaleQ / xScaleP * xUnit;
            xMaxOrg = xMax * xScaleQ / xScaleP * xUnit;
            yMinOrg = yMin * yScaleQ / yScaleP;
            yMaxOrg = yMax * yScaleQ / yScaleP;
            xSector = xMax - xMin;
            ySector = yMax - yMin;
            Index2draw = -1;
        }

        int xMinSave, xMaxSave, yMinSave, yMaxSave, xScaleSave, yScaleSave, yFactorSave;
        long xFactorSave;
        public void SaveScale()
        {
            xMinSave = xMin * xUnit; xMaxSave = xMax * xUnit;
            yMinSave = yMin; yMaxSave = yMax;
            xScaleSave = xScaleP * xUnit; yScaleSave = yScaleP;
            xFactorSave = xFactor; yFactorSave = yFactor;
        }

        int xMinUndo, xMaxUndo, xUnitUndo, yMinUndo, yMaxUndo;
        public bool undoPossible = false;
        public void UndoAutoscale()
        {
            xMin = xMinUndo; xMax = xMaxUndo; xUnit = xUnitUndo;
            yMin = yMinUndo; yMax = yMaxUndo;
            undoPossible = false;
            drawMode = DrawMode.Draw;
            UpdateScaling(false);
        }

        public void DrawGraph(Graphics g, Bitmap BackBuffer, Graphics BackBufferGraphics)
        {
            int PlotCount = parent.PlotCount;
            int Plot2ndCount = parent.Plot2ndCount;

            Pen p = new Pen(Color.Gray, 1);
            SolidBrush b = new SolidBrush(parent.GetLineColor(parent.comboBoxKmlOptColor));
            Font f = new Font("Tahoma", 8F, System.Drawing.FontStyle.Regular);
            Font f2 = new Font("Tahoma", 12F, System.Drawing.FontStyle.Bold);

            int x0 = BackBuffer.Width / 24;        //10   4.16%
            int y0 = BackBuffer.Height * 472 / 508;     //h-18

            int Psize = parent.GetLineWidth(parent.comboBoxKmlOptWidth);
            bool intersectionValid = false;

            Debug.Write(scaleCmd.ToString() + " " + drawMode);
            if (parent.CurrentPlotIndex >= 0 || PlotCount > 0 || (y2 != null && Plot2ndCount > 0))
            {
                if (alignT2f && parent.CurrentPlotIndex >= 0 && y2 != null && Plot2ndCount > 0)
                {
                    GetIntersection();
                    intersectionValid = true;
                    x2Ofs = x[parent.CurrentPlotIndex] - xs;
                }
                else x2Ofs = 0;

                if (scaleCmd == ScaleCmd.DoAutoscale || scaleCmd == ScaleCmd.DoAutoscaleNoUndo)
                    Autoscale(true);
                else if (scaleCmd == ScaleCmd.DoYAutoscale)
                    Autoscale(false);
                else if (scaleCmd == ScaleCmd.DoRedraw)
                    Index2draw = -1;
                scaleCmd = ScaleCmd.Nothing;

                int xEdgeDist, yEdgeDist;
                if (y2 != null && Plot2ndCount > 1)     //keep 1/3 of "T2F in advance" visible
                {
                    xEdgeDist = (xMaxOrg - xMinOrg) / 3;
                    yEdgeDist = (yMaxOrg - yMinOrg) / 3;
                }
                else
                {
                    xEdgeDist = 0;
                    yEdgeDist = 0;
                }

                if (parent.CurrentPlotIndex >= 0 && moveState == MoveState.Nothing)
                {
                    if (drawMode == DrawMode.KeepAutoscale)
                    {
                        if (x[parent.CurrentPlotIndex] > xMaxOrg || y[parent.CurrentPlotIndex] > yMaxOrg || y[parent.CurrentPlotIndex] < yMinOrg
                        || Plot2ndCount > 0 && (x2[0] + x2Ofs < xMinOrg || x2[Plot2ndCount - 1] + x2Ofs > xMaxOrg || Math.Abs(x2Ofs - x2OfsDrawn) > (xMaxOrg - xMinOrg) / 32))
                            Autoscale(true);
                    }
                    else if (drawMode == DrawMode.Track)
                    {
                        bool needUpdateScaling = false;
                        
                        while (x[parent.CurrentPlotIndex] + xEdgeDist > xMaxOrg)
                        {
                            xMax += xDiv;
                            xMin += xDiv;
                            xMaxOrg = xMax * xScaleQ / xScaleP * xUnit;
                            needUpdateScaling = true;
                        }
                        while (x[parent.CurrentPlotIndex] < xMinOrg)
                        {
                            xMax -= xDiv;
                            xMin -= xDiv;
                            xMinOrg = xMin * xScaleQ / xScaleP * xUnit;
                            needUpdateScaling = true;
                        }
                        while (y[parent.CurrentPlotIndex] + yEdgeDist > yMaxOrg)
                        {
                            yMax += yDiv;
                            yMin += yDiv;
                            yMaxOrg = yMax * yScaleQ / yScaleP;
                            needUpdateScaling = true;
                        }
                        while (y[parent.CurrentPlotIndex] - yEdgeDist < yMinOrg)
                        {
                            yMin -= yDiv;
                            yMax -= yDiv;
                            yMinOrg = yMin * yScaleQ / yScaleP;
                            needUpdateScaling = true;
                        }
                        if (needUpdateScaling || Math.Abs(x2Ofs - x2OfsDrawn) > (xMaxOrg - xMinOrg) / 32)
                            UpdateScaling(true);
                    }
                }

                if (moveState == MoveState.Move || moveState == MoveState.MoveEnd)
                {
                    int xDelta = parent.MouseShiftX * (xMaxSave - xMinSave) / (int)xFactor * xScaleP / xScaleQ;     //test XScale
                    int yDelta = parent.MouseShiftY * (yMaxSave - yMinSave) / yFactor * yScaleP / yScaleQ;
                    if (moveState == MoveState.MoveEnd)
                        moveState = MoveState.Nothing;
                    bool xChanged = false, yChanged = false;
                    if (Math.Abs(parent.MouseShiftX) > BackBuffer.Width / 20)
                        xChanged = true;
                    if (Math.Abs(parent.MouseShiftY) > BackBuffer.Height / 20)
                        yChanged = true;
                    bool shift;
                    if (mousePos == MousePos.middle)    //shift
                    {
                        int vz;
                        if (xChanged)
                        {
                            if (parent.MouseShiftX < 0) vz = -1; else vz = 1;
                            xDelta = (xDelta * vz / xDiv) * xDiv * vz;
                            xMin = xMinSave - xDelta;
                            xMax = xMaxSave - xDelta;
                            xUnit = 1;
                        }
                        if (yChanged)
                        {
                            if (parent.MouseShiftY < 0) vz = -1; else vz = 1;
                            yDelta = (yDelta * vz / yDiv) * yDiv * vz;
                            yMin = yMinSave + yDelta;
                            yMax = yMaxSave + yDelta;
                        }
                        shift = true;
                    }
                    else        //zoom
                    {
                        xMax = xMaxSave; xMin = xMinSave; xUnit = 1;        //return to default unit
                        if (xChanged)
                        {
                            if (parent.MouseClientX < BackBuffer.Width * 40 / 480) { xMin = xMinSave - xDelta; }      //20
                            else if (parent.MouseClientX > BackBuffer.Width * 440 / 480) { xMax = xMaxSave - xDelta; }    //w-20
                            else if (parent.MouseShiftX > 0) { xMax = xMaxSave - xDelta; }
                            else { xMin = xMinSave - xDelta; }
                        }
                        if (yChanged)
                        {
                            if (parent.MouseClientY < BackBuffer.Width * 40 / 480) { yMax = yMaxSave + yDelta; }    //20
                            else if (parent.MouseClientY > BackBuffer.Height * 460 / 508) { yMin = yMinSave + yDelta; }    //h-24
                            else if (parent.MouseShiftY > 0) { yMin = yMinSave + yDelta; }
                            else { yMax = yMaxSave + yDelta; }
                        }
                        shift = false;
                    }
                    if (xChanged || yChanged)
                    {
                        UpdateScaling(shift);
                        if (drawMode == DrawMode.KeepAutoscale)
                            drawMode = DrawMode.Track;
                        if (parent.CurrentPlotIndex >= 0 && (x[parent.CurrentPlotIndex] + xEdgeDist > xMaxOrg || x[parent.CurrentPlotIndex] < xMinOrg || y[parent.CurrentPlotIndex] + yEdgeDist > yMaxOrg || y[parent.CurrentPlotIndex] - yEdgeDist < yMinOrg))
                            drawMode = DrawMode.Draw;
                    }
                    else
                        Index2draw = -1;
                }


                if (Index2draw < 0)
                {
                    Debug.WriteLine("Draw full");
                    Index2draw = 0;
                    BackBufferGraphics.Clear(Form1.bkColor);

                    //Draw grid
                    int xFac = parent.NoBkPanel.Width *  11 / 12;
                    int yFac = parent.NoBkPanel.Height * 9 / 10;
                    for (int i = xMin; i <= xMax; i += xDiv)
                    {
                        BackBufferGraphics.DrawLine(p, x0 + (int)((i - xMin) * xFac / xSector), y0, x0 + (int)((i - xMin) * xFac / xSector), y0 - (yMax - yMin) * yFac / ySector);
                    }
                    for (int i = yMin; i <= yMax; i += yDiv)
                    {
                        BackBufferGraphics.DrawLine(p, x0, y0 - (i - yMin) * yFac / ySector, x0 + (int)((xMax - xMin) * xFac / xSector), y0 - (i - yMin) * yFac / ySector);
                    }

                    //Draw text
                    String xMaxStr = xMax.ToString();
                    SizeF size = BackBufferGraphics.MeasureString(xMaxStr, f);
                    BackBufferGraphics.DrawString(xMin.ToString(), f, b, BackBuffer.Width * 4 / 480, BackBuffer.Height * 480 / 508);
                    BackBufferGraphics.DrawString(xMaxStr, f, b, BackBuffer.Width * 476 / 480 - size.Width, BackBuffer.Height * 480 / 508);
                    BackBufferGraphics.DrawString(xLabel1 + xDiv.ToString() + xLabel2, f, b, BackBuffer.Width * 160 / 480, BackBuffer.Height * 480 / 508);
                    BackBufferGraphics.DrawString(yMax.ToString(), f, b, BackBuffer.Width * 2 / 480, BackBuffer.Height * 4 / 508);
                    BackBufferGraphics.DrawString(yMin.ToString(), f, b, BackBuffer.Width * 2 / 480, BackBuffer.Height * 460 / 508);
                    BackBufferGraphics.DrawString(title1 + yDiv.ToString() + title2, f2, b, BackBuffer.Width * 120 / 480, BackBuffer.Height * 4 / 508);
                    String drawModeStr = null; ;
                    if (drawMode == DrawMode.KeepAutoscale)
                        drawModeStr = "auto";
                    else if (drawMode == DrawMode.Track)
                        drawModeStr = "track";
                    if (drawModeStr != null)
                    {
                        size = BackBufferGraphics.MeasureString(drawModeStr, f);
                        BackBufferGraphics.DrawString(drawModeStr, f, b, BackBuffer.Width * 460 / 480 - size.Width, BackBuffer.Height * 14 / 508);
                    }

                    if (y2 != null && Plot2ndCount > 0 && !hideT2f)
                    {                    //Draw line T2F
                        p.Color = parent.GetLineColor(parent.comboBoxLine2OptColor);
                        p.Width = parent.GetLineWidth(parent.comboBoxLine2OptWidth) / 2;
                        if (alignT2f)
                        {
                            b.Color = p.Color;
                            String x2OfsStr = (x2Ofs * xScaleP / (xScaleQ * xUnit)).ToString();
                            size = BackBufferGraphics.MeasureString(x2OfsStr, f);
                            BackBufferGraphics.DrawString(x2OfsStr, f, b, BackBuffer.Width * 460 / 480 - size.Width, BackBuffer.Height * 448 / 508);
                        }
                        x2OfsDrawn = x2Ofs;

                        int j1, j2;
                        for (j1 = 0; j1 < Plot2ndCount - 1; j1++)   //ignore not visible
                            if (x2[j1 + 1] + x2OfsDrawn >= xMinOrg) break;
                        while (y2[j1] == Int16.MinValue)     //ignore invalids at the beginning
                        {
                            j1++;
                            if (j1 >= Plot2ndCount) break;
                        }
                        if (j1 >= Plot2ndCount - 1) j2 = j1;    //draw single point  - working?
                        else j2 = j1 + 1;
                        for (; j2 < Plot2ndCount; j2++)
                        {
                            while (y2[j2] == Int16.MinValue)
                            {
                                if (++j2 >= Plot2ndCount) goto exit2;
                            }
                            if (x2[j1] + x2OfsDrawn > xMaxOrg) goto exit2;      //not visible
                            BackBufferGraphics.DrawLine(p, x0 + (int)((x2[j1] - xMinOrg + x2OfsDrawn) * xFactor / xUnit / xSector), y0 - (y2[j1] - yMinOrg) * yFactor / ySector,
                                                           x0 + (int)((x2[j2] - xMinOrg + x2OfsDrawn) * xFactor / xUnit / xSector), y0 - (y2[j2] - yMinOrg) * yFactor / ySector);
                            j1 = j2;
                        }
                    exit2: ;
                    }
                }

                p.Color = parent.GetLineColor(parent.comboBoxKmlOptColor);
                if (PlotCount > 0 && !hideTrack)
                {
                    Debug.WriteLine(Index2draw);
                    //Draw line
                    p.Width = Psize / 2;
                    int i1 = Index2draw, i2;
                    for (i1 = 0; i1 < PlotCount - 1; i1++)   //ignore not visible
                        if (x[i1 + 1] >= xMinOrg) break;
                    while (y[i1] == Int16.MinValue)     //ignore invalids at the beginning
                    {
                        i1++;
                        if (i1 >= PlotCount) break;
                    }
                    if (i1 >= PlotCount - 1) i2 = i1;    //draw single point
                    else i2 = i1 + 1;
                    for (; i2 < PlotCount; i2++)
                    {
                        while (y[i2] == Int16.MinValue)
                        {
                            if (++i2 >= PlotCount) goto exit;
                        }
                        if (x[i1] > xMaxOrg) goto exit;      //not visible
                        BackBufferGraphics.DrawLine(p, x0 + (int)((x[i1] - xMinOrg) * xFactor / xUnit / xSector), y0 - (y[i1] - yMinOrg) * yFactor / ySector,
                                                       x0 + (int)((x[i2] - xMinOrg) * xFactor / xUnit / xSector), y0 - (y[i2] - yMinOrg) * yFactor / ySector);
                        Index2draw = i2;
                        i1 = i2;
                    }
                exit: ;
                }
            }
            else
            {
                BackBufferGraphics.Clear(Form1.bkColor);
                BackBufferGraphics.DrawString(title1 + title2, f2, b, BackBuffer.Width * 120 / 480, BackBuffer.Height * 4 / 508);
                BackBufferGraphics.DrawString("no data to plot", f2, b, BackBuffer.Width * 20 / 480, BackBuffer.Height * 80 / 508);
            }
            g.DrawImage(BackBuffer, 0, 0); // draw back buffer on screen
            //Draw current point
            if (y2 != null && Plot2ndCount > 0 && !hideT2f)     //draw point with index of intersection to t2f
            {
                if (!intersectionValid)
                    GetIntersection();
                b.Color = GpsUtils.Utils.modifyColor(parent.GetLineColor(parent.comboBoxLine2OptColor), +180);
                g.FillEllipse(b, x0 + (int)((xs - xMinOrg + x2OfsDrawn) * xFactor / xUnit / xSector) - Psize, y0 - (ys - yMinOrg) * yFactor / ySector - Psize, 2 * Psize, 2 * Psize);
            }
            if (parent.CurrentPlotIndex >= 0 && !hideTrack)        //draw current point
            {
                b.Color = p.Color;
                g.FillEllipse(b, x0 + (int)((x[parent.CurrentPlotIndex] - xMinOrg) * xFactor / xUnit / xSector) - Psize, y0 - (y[parent.CurrentPlotIndex] - yMinOrg) * yFactor / ySector - Psize, 2 * Psize, 2 * Psize);
            }
        }

        int xs, ys, x2Ofs, x2OfsDrawn;
        private void GetIntersection()
        {
            parent.mapUtil.GetNavigationData(parent.Plot2ndLong, parent.Plot2ndLat, parent.Plot2ndCount, parent.CurrentLong, parent.CurrentLat);
            parent.mapUtil.DoVoiceCommand();
            int index = (int)parent.mapUtil.nav.ixd_intersec;
            xs = x2[index];
            ys = y2[index];
            double fraction = parent.mapUtil.nav.ixd_intersec - index;
            if (fraction > 0)
            {
                xs += (int)((x2[index + 1] - x2[index]) * fraction);
                ys += (int)((y2[index + 1] - y2[index]) * fraction);
            }
        }

        private int RoundMinMax(ref int aMin, ref int aMax)
        {
            int div;
            int a = aMax - aMin;
            if (a < 100) div = 1;           //div is minimal 1
            else if (a < 1000) div = 10;
            else if (a < 10000) div = 100;
            else div = 1000;

            a = a / div;

            if (a < 10) div *= 1;
            else if (a < 20) div *= 2;
            else if (a < 50) div *= 5;
            else div *= 10;

            int aMinExact = aMin;
            aMin = (aMin / div) * div;
            if (aMinExact < aMin) aMin -= div;
            a = (aMax / div) * div;
            if (a != aMax || a == aMin)
                aMax = a + div;             //aMax is minimal aMin + 1

            return div;
        }


    }
}
