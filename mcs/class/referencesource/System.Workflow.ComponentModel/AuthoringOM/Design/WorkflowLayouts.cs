namespace System.Workflow.ComponentModel.Design
{
    using System;
    using System.Drawing;
    using System.Diagnostics;
    using System.Collections;
    using System.Windows.Forms;
    using System.Drawing.Imaging;
    using System.Drawing.Printing;
    using System.Drawing.Drawing2D;
    using System.ComponentModel.Design;

    #region Class WorkflowLayout
    //All the coordinates and sizes are in logical coordinate system
    internal abstract class WorkflowLayout : IDisposable
    {
        #region Members and Constructor/Destruction
        [Obsolete("The System.Workflow.* types are deprecated.  Instead, please use the new types from System.Activities.*")]
        public enum LayoutUpdateReason { LayoutChanged, ZoomChanged }
        protected IServiceProvider serviceProvider;
        protected WorkflowView parentView;

        public WorkflowLayout(IServiceProvider serviceProvider)
        {
            Debug.Assert(serviceProvider != null);
            if (serviceProvider == null)
                throw new ArgumentNullException("serviceProvider");

            this.serviceProvider = serviceProvider;

            this.parentView = this.serviceProvider.GetService(typeof(WorkflowView)) as WorkflowView;
            Debug.Assert(this.parentView != null);
            if (this.parentView == null)
                throw new InvalidOperationException(SR.GetString(SR.General_MissingService, typeof(WorkflowView).FullName));
        }

        public virtual void Dispose()
        {
        }
        #endregion

        #region Public Interface
        public abstract float Scaling { get; }
        public abstract Size Extent { get; }
        public abstract Point RootDesignerAlignment { get; }

        public abstract bool IsCoOrdInLayout(Point logicalCoOrd);
        public abstract Rectangle MapInRectangleToLayout(Rectangle logicalRectangle);
        public abstract Rectangle MapOutRectangleFromLayout(Rectangle logicalRectangle);
        public abstract Point MapInCoOrdToLayout(Point logicalPoint);
        public abstract Point MapOutCoOrdFromLayout(Point logicalPoint);

        public abstract void OnPaint(PaintEventArgs e, ViewPortData viewPortData);
        public abstract void OnPaintWorkflow(PaintEventArgs e, ViewPortData viewPortData);
        public abstract void Update(Graphics graphics, LayoutUpdateReason reason);
        #endregion
    }
    #endregion

    #region Class DefaultWorkflowLayout: For rendering Root without any customization
    internal abstract class DefaultWorkflowLayout : WorkflowLayout
    {
        #region Members and Constructor
        public static Size Separator = new Size(30, 30);

        public DefaultWorkflowLayout(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }
        #endregion

        #region WorkflowLayout Overrides
        public override float Scaling
        {
            get
            {
                return 1.0f;
            }
        }

        public override Size Extent
        {
            get
            {
                Size rootDesignerSize = (this.parentView.RootDesigner != null) ? this.parentView.RootDesigner.Size : Size.Empty;
                Size totalSize = new Size(rootDesignerSize.Width + DefaultWorkflowLayout.Separator.Width * 2, rootDesignerSize.Height + DefaultWorkflowLayout.Separator.Height * 2);
                Size clientSize = this.parentView.ViewPortSize;
                return new Size(Math.Max(totalSize.Width, clientSize.Width), Math.Max(totalSize.Height, clientSize.Height));
            }
        }

        public override Point RootDesignerAlignment
        {
            get
            {
                return new Point(DefaultWorkflowLayout.Separator);
            }
        }

        public override bool IsCoOrdInLayout(Point logicalCoOrd)
        {
            return true;
        }

        public override Rectangle MapInRectangleToLayout(Rectangle logicalRectangle)
        {
            return logicalRectangle;
        }

        public override Rectangle MapOutRectangleFromLayout(Rectangle logicalRectangle)
        {
            return logicalRectangle;
        }

        public override Point MapInCoOrdToLayout(Point logicalPoint)
        {
            return logicalPoint;
        }

        public override Point MapOutCoOrdFromLayout(Point logicalPoint)
        {
            return logicalPoint;
        }

        public override void Update(Graphics graphics, LayoutUpdateReason reason)
        {
            //We dont do anything as our layout is simple
        }

        //

        public override void OnPaint(PaintEventArgs e, ViewPortData viewPortData)
        {
            Graphics graphics = e.Graphics;
            Debug.Assert(graphics != null);

            //Get the drawing canvas
            Bitmap memoryBitmap = viewPortData.MemoryBitmap;
            Debug.Assert(memoryBitmap != null);

            //Fill the background using the workspace color so that we communicate the paging concept
            Rectangle workspaceRectangle = new Rectangle(Point.Empty, memoryBitmap.Size);
            graphics.FillRectangle(AmbientTheme.WorkspaceBackgroundBrush, workspaceRectangle);
            if (this.parentView.RootDesigner != null &&
                this.parentView.RootDesigner.Bounds.Width >= 0 && this.parentView.RootDesigner.Bounds.Height >= 0)
            {
                GraphicsContainer graphicsState = graphics.BeginContainer();

                //Create the scaling matrix 
                Matrix transformationMatrix = new Matrix();
                transformationMatrix.Scale(viewPortData.Scaling.Width, viewPortData.Scaling.Height, MatrixOrder.Prepend);

                //When we draw on the viewport we draw in scaled and translated. 
                //So that we minimize the calls to DrawImage
                //Make sure that we scale down the logical view port origin in order to take care of scaling factor
                //Before we select the transform factor we make sure that logicalviewport origin is scaled down
                Point[] logicalViewPortOrigin = new Point[] { viewPortData.LogicalViewPort.Location };
                transformationMatrix.TransformPoints(logicalViewPortOrigin);

                //For performance improvement and to eliminate one extra DrawImage...we draw the designers on the viewport
                //bitmap with visual depth consideration
                transformationMatrix.Translate(-logicalViewPortOrigin[0].X + viewPortData.ShadowDepth.Width, -logicalViewPortOrigin[0].Y + viewPortData.ShadowDepth.Height, MatrixOrder.Append);

                //Select the transform into viewport graphics.
                //Viewport bitmap has the scaled and translated designers which we then map to
                //the actual graphics based on page layout
                graphics.Transform = transformationMatrix;

                using (Region clipRegion = new Region(ActivityDesignerPaint.GetDesignerPath(this.parentView.RootDesigner, false)))
                {
                    Region oldRegion = graphics.Clip;
                    graphics.Clip = clipRegion;

                    AmbientTheme ambientTheme = WorkflowTheme.CurrentTheme.AmbientTheme;
                    graphics.FillRectangle(Brushes.White, this.parentView.RootDesigner.Bounds);

                    if (ambientTheme.WorkflowWatermarkImage != null)
                        ActivityDesignerPaint.DrawImage(graphics, ambientTheme.WorkflowWatermarkImage, this.parentView.RootDesigner.Bounds, new Rectangle(Point.Empty, ambientTheme.WorkflowWatermarkImage.Size), ambientTheme.WatermarkAlignment, AmbientTheme.WatermarkTransparency, false);

                    graphics.Clip = oldRegion;
                }

                graphics.EndContainer(graphicsState);
            }
        }

        public override void OnPaintWorkflow(PaintEventArgs e, ViewPortData viewPortData)
        {
            Graphics graphics = e.Graphics;
            Debug.Assert(graphics != null);

            //Get the drawing canvas
            Bitmap memoryBitmap = viewPortData.MemoryBitmap;
            Debug.Assert(memoryBitmap != null);
            Rectangle bitmapArea = new Rectangle(Point.Empty, memoryBitmap.Size);
            ActivityDesignerPaint.DrawImage(graphics, memoryBitmap, bitmapArea, bitmapArea, DesignerContentAlignment.Fill, 1.0f, WorkflowTheme.CurrentTheme.AmbientTheme.DrawGrayscale);
        }
        #endregion
    }
    #endregion

    #region Class ActivityRootLayout: For rendering when Activity is Root
    internal sealed class ActivityRootLayout : DefaultWorkflowLayout
    {
        #region Members and Constructor
        internal ActivityRootLayout(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }
        #endregion

        public override Size Extent
        {
            get
            {
                Size rootDesignerSize = (this.parentView.RootDesigner != null) ? this.parentView.RootDesigner.Size : Size.Empty;
                Size totalSize = new Size(rootDesignerSize.Width + DefaultWorkflowLayout.Separator.Width * 2, rootDesignerSize.Height + DefaultWorkflowLayout.Separator.Height * 2);
                Size clientSize = this.parentView.ViewPortSize;
                //since the activity designer doesnt take the full viewport area, we need to scale available viewport back by the zoom factor
                clientSize.Width = (int)(clientSize.Width / ((float)this.parentView.Zoom / 100.0f));
                clientSize.Height = (int)(clientSize.Height / ((float)this.parentView.Zoom / 100.0f));

                return new Size(Math.Max(totalSize.Width, clientSize.Width), Math.Max(totalSize.Height, clientSize.Height));
            }
        }

        public override void OnPaint(PaintEventArgs e, ViewPortData viewPortData)
        {
            base.OnPaint(e, viewPortData);

            Graphics graphics = e.Graphics;
            if (this.parentView.RootDesigner != null &&
                this.parentView.RootDesigner.Bounds.Width >= 0 && this.parentView.RootDesigner.Bounds.Height >= 0)
            {
                GraphicsContainer graphicsState = graphics.BeginContainer();

                //Create the scaling matrix 
                Matrix transformationMatrix = new Matrix();
                transformationMatrix.Scale(viewPortData.Scaling.Width, viewPortData.Scaling.Height, MatrixOrder.Prepend);

                //When we draw on the viewport we draw in scaled and translated. 
                //So that we minimize the calls to DrawImage
                //Make sure that we scale down the logical view port origin in order to take care of scaling factor
                //Before we select the transform factor we make sure that logicalviewport origin is scaled down
                Point[] logicalViewPortOrigin = new Point[] { viewPortData.LogicalViewPort.Location };
                transformationMatrix.TransformPoints(logicalViewPortOrigin);

                //For performance improvement and to eliminate one extra DrawImage...we draw the designers on the viewport
                //bitmap with visual depth consideration
                transformationMatrix.Translate(-logicalViewPortOrigin[0].X + viewPortData.ShadowDepth.Width, -logicalViewPortOrigin[0].Y + viewPortData.ShadowDepth.Height, MatrixOrder.Append);

                //Select the transform into viewport graphics.
                //Viewport bitmap has the scaled and translated designers which we then map to
                //the actual graphics based on page layout
                graphics.Transform = transformationMatrix;

                Rectangle rootBounds = this.parentView.RootDesigner.Bounds;
                graphics.ExcludeClip(rootBounds);
                rootBounds.Inflate(ActivityRootLayout.Separator.Width / 2, ActivityRootLayout.Separator.Height / 2);
                ActivityDesignerPaint.DrawDropShadow(graphics, rootBounds, AmbientTheme.WorkflowBorderPen.Color, AmbientTheme.DropShadowWidth, LightSourcePosition.Left | LightSourcePosition.Top, 0.2f, false);

                graphics.FillRectangle(WorkflowTheme.CurrentTheme.AmbientTheme.BackgroundBrush, rootBounds);
                graphics.DrawRectangle(AmbientTheme.WorkflowBorderPen, rootBounds);

                graphics.EndContainer(graphicsState);
            }
        }
    }
    #endregion

    #region Class WorkflowRootLayout: For rendering when Sequential Workflow is Root, by always centering it
    internal sealed class WorkflowRootLayout : DefaultWorkflowLayout
    {
        #region Members and Constructor
        public WorkflowRootLayout(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }
        #endregion

        #region WorkflowLayout Overrides
        public override Rectangle MapInRectangleToLayout(Rectangle logicalRectangle)
        {
            Size offSet = Offset;
            logicalRectangle.X -= offSet.Width;
            logicalRectangle.Y -= offSet.Height;
            return logicalRectangle;
        }

        public override Rectangle MapOutRectangleFromLayout(Rectangle logicalRectangle)
        {
            Size offSet = Offset;
            logicalRectangle.X += offSet.Width;
            logicalRectangle.Y += offSet.Height;
            return logicalRectangle;
        }

        public override Point MapInCoOrdToLayout(Point logicalPoint)
        {
            Size offSet = Offset;
            logicalPoint.Offset(-offSet.Width, -offSet.Height);
            return logicalPoint;
        }

        public override Point MapOutCoOrdFromLayout(Point logicalPoint)
        {
            Size offSet = Offset;
            logicalPoint.Offset(offSet.Width, offSet.Height);
            return logicalPoint;
        }
        #endregion

        #region Helpers
        private Size Offset
        {
            get
            {
                //This logic is needed in order to keep the service root designer centered
                Size layoutExtent = Extent;
                Size totalSize = this.parentView.ClientSizeToLogical(this.parentView.ViewPortSize);
                totalSize.Width = Math.Max(totalSize.Width, layoutExtent.Width);
                totalSize.Height = Math.Max(totalSize.Height, layoutExtent.Height);
                return new Size(Math.Max(0, (totalSize.Width - layoutExtent.Width) / 2), Math.Max(0, (totalSize.Height - layoutExtent.Height) / 2));
            }
        }
        #endregion
    }
    #endregion

    #region Class PrintPreviewLayout: For rendering print preview layout
    internal sealed class PrintPreviewLayout : WorkflowLayout
    {
        #region Members and Constructor
        private static Size DefaultPageSeparator = new Size(30, 30);
        private static Margins DefaultPageMargins = new Margins(20, 20, 20, 20);

        private WorkflowPrintDocument printDocument = null;
        private ArrayList pageLayoutInfo = new ArrayList();

        //We calculate the following variables when we perform layout, we store these variables
        //so that drawing will be faster
        private Margins headerFooterMargins = new Margins(0, 0, 0, 0);
        private Size pageSeparator = PrintPreviewLayout.DefaultPageSeparator;
        private Margins pageMargins = PrintPreviewLayout.DefaultPageMargins;
        private Size rowColumns = new Size(1, 1); //Width = Columns, Height = Rows
        private float scaling = 1.0f;
        private Size pageSize = Size.Empty;
        private DateTime previewTime = DateTime.Now;

        internal PrintPreviewLayout(IServiceProvider serviceProvider, WorkflowPrintDocument printDoc)
            : base(serviceProvider)
        {
            this.printDocument = printDoc;
        }
        #endregion

        #region WorkflowLayout Overrides
        public override float Scaling
        {
            get
            {
                return this.scaling;
            }
        }

        public override Size Extent
        {
            get
            {
                //RowColumns Width = Columns, Height = Rows
                Size maxSize = Size.Empty;
                maxSize.Width = (this.rowColumns.Width * this.pageSize.Width) + ((this.rowColumns.Width + 1) * (PageSeparator.Width));
                maxSize.Height = (this.rowColumns.Height * this.pageSize.Height) + ((this.rowColumns.Height + 1) * (PageSeparator.Height));
                return maxSize;
            }
        }

        public override Point RootDesignerAlignment
        {
            get
            {
                Point alignment = Point.Empty;
                Size printableAreaPerPage = new Size(this.pageSize.Width - (PageMargins.Left + PageMargins.Right), this.pageSize.Height - (PageMargins.Top + PageMargins.Bottom));
                Size totalPrintableArea = new Size(this.rowColumns.Width * printableAreaPerPage.Width, this.rowColumns.Height * printableAreaPerPage.Height);
                Size rootDesignerSize = (this.parentView.RootDesigner != null) ? this.parentView.RootDesigner.Size : Size.Empty;
                Size selectionSize = WorkflowTheme.CurrentTheme.AmbientTheme.SelectionSize;

                if (this.printDocument.PageSetupData.CenterHorizontally)
                    alignment.X = (totalPrintableArea.Width - rootDesignerSize.Width) / 2;
                alignment.X = Math.Max(alignment.X, selectionSize.Width + selectionSize.Width / 2);

                if (this.printDocument.PageSetupData.CenterVertically)
                    alignment.Y = (totalPrintableArea.Height - rootDesignerSize.Height) / 2;
                alignment.Y = Math.Max(alignment.Y, selectionSize.Height + selectionSize.Height / 2);

                return alignment;
            }
        }

        public override bool IsCoOrdInLayout(Point logicalCoOrd)
        {
            foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
            {
                if (pageLayoutData.ViewablePageBounds.Contains(logicalCoOrd))
                    return true;
            }

            return false;
        }

        public override Rectangle MapInRectangleToLayout(Rectangle logicalRectangle)
        {
            Rectangle transformedViewPort = Rectangle.Empty;

            //Now we start mapping the rectangle based on page layout
            foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
            {
                Rectangle intersectedPhysicalViewPort = logicalRectangle;
                intersectedPhysicalViewPort.Intersect(pageLayoutData.ViewablePageBounds);
                if (!intersectedPhysicalViewPort.IsEmpty)
                {
                    Point deltaLocation = new Point(intersectedPhysicalViewPort.X - pageLayoutData.ViewablePageBounds.X, intersectedPhysicalViewPort.Y - pageLayoutData.ViewablePageBounds.Y);

                    Size deltaSize = new Size(pageLayoutData.ViewablePageBounds.Width - intersectedPhysicalViewPort.Width, pageLayoutData.ViewablePageBounds.Height - intersectedPhysicalViewPort.Height);
                    deltaSize.Width -= deltaLocation.X;
                    deltaSize.Height -= deltaLocation.Y;

                    //Get the intersecting rectangle
                    Rectangle insersectedLogicalViewPort = Rectangle.Empty;
                    insersectedLogicalViewPort.X = pageLayoutData.LogicalPageBounds.X + deltaLocation.X;
                    insersectedLogicalViewPort.Y = pageLayoutData.LogicalPageBounds.Y + deltaLocation.Y;

                    insersectedLogicalViewPort.Width = pageLayoutData.LogicalPageBounds.Width - deltaLocation.X;
                    insersectedLogicalViewPort.Width -= deltaSize.Width;

                    insersectedLogicalViewPort.Height = pageLayoutData.LogicalPageBounds.Height - deltaLocation.Y;
                    insersectedLogicalViewPort.Height -= deltaSize.Height;

                    transformedViewPort = (transformedViewPort.IsEmpty) ? insersectedLogicalViewPort : Rectangle.Union(transformedViewPort, insersectedLogicalViewPort);
                }
            }

            return transformedViewPort;
        }

        public override Rectangle MapOutRectangleFromLayout(Rectangle logicalRectangle)
        {
            Rectangle transformedViewPort = Rectangle.Empty;

            //Now we start mapping the rectangle based on page layout
            foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
            {
                Rectangle intersectedLogicalViewPort = logicalRectangle;
                intersectedLogicalViewPort.Intersect(pageLayoutData.LogicalPageBounds);
                if (!intersectedLogicalViewPort.IsEmpty)
                {
                    Point deltaLocation = new Point(intersectedLogicalViewPort.X - pageLayoutData.LogicalPageBounds.X, intersectedLogicalViewPort.Y - pageLayoutData.LogicalPageBounds.Y);

                    Size deltaSize = new Size(pageLayoutData.LogicalPageBounds.Width - intersectedLogicalViewPort.Width, pageLayoutData.LogicalPageBounds.Height - intersectedLogicalViewPort.Height);
                    deltaSize.Width -= deltaLocation.X;
                    deltaSize.Height -= deltaLocation.Y;

                    //Get the intersecting rectangle
                    Rectangle insersectedPhysicalViewPort = Rectangle.Empty;
                    insersectedPhysicalViewPort.X = pageLayoutData.ViewablePageBounds.X + deltaLocation.X;
                    insersectedPhysicalViewPort.Y = pageLayoutData.ViewablePageBounds.Y + deltaLocation.Y;

                    insersectedPhysicalViewPort.Width = pageLayoutData.ViewablePageBounds.Width - deltaLocation.X;
                    insersectedPhysicalViewPort.Width -= deltaSize.Width;

                    insersectedPhysicalViewPort.Height = pageLayoutData.ViewablePageBounds.Height - deltaLocation.Y;
                    insersectedPhysicalViewPort.Height -= deltaSize.Height;

                    transformedViewPort = (transformedViewPort.IsEmpty) ? insersectedPhysicalViewPort : Rectangle.Union(transformedViewPort, insersectedPhysicalViewPort);
                }
            }

            return transformedViewPort;
        }

        public override Point MapInCoOrdToLayout(Point logicalPoint)
        {
            //Only for default layout we scale the coordinates outside the pageboundry for all other cases scaling fails
            foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
            {
                if (pageLayoutData.PageBounds.Contains(logicalPoint))
                {
                    Point delta = new Point(logicalPoint.X - pageLayoutData.ViewablePageBounds.Left, logicalPoint.Y - pageLayoutData.ViewablePageBounds.Top);
                    logicalPoint = new Point(pageLayoutData.LogicalPageBounds.Left + delta.X, pageLayoutData.LogicalPageBounds.Top + delta.Y);
                    break;
                }
            }

            return logicalPoint;
        }

        public override Point MapOutCoOrdFromLayout(Point logicalPoint)
        {
            foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
            {
                if (pageLayoutData.LogicalPageBounds.Contains(logicalPoint))
                {
                    Point delta = new Point(logicalPoint.X - pageLayoutData.LogicalPageBounds.Left, logicalPoint.Y - pageLayoutData.LogicalPageBounds.Top);
                    logicalPoint = new Point(pageLayoutData.ViewablePageBounds.Left + delta.X, pageLayoutData.ViewablePageBounds.Top + delta.Y);
                    break;
                }
            }

            return logicalPoint;
        }

        public override void OnPaint(PaintEventArgs e, ViewPortData viewPortData)
        {
            Graphics graphics = e.Graphics;
            Debug.Assert(graphics != null);
            AmbientTheme ambientTheme = WorkflowTheme.CurrentTheme.AmbientTheme;

            //Get the drawing canvas
            Bitmap memoryBitmap = viewPortData.MemoryBitmap;
            Debug.Assert(memoryBitmap != null);

            //Fill the background using the workspace color so that we communicate the paging concept
            graphics.FillRectangle(Brushes.White, new Rectangle(Point.Empty, memoryBitmap.Size));

            //Fill the background using the workspace color so that we communicate the paging concept
            //if there is no workflow watermark, just return
            if (ambientTheme.WorkflowWatermarkImage == null)
                return;

            //Create the transformation matrix and calculate the physical viewport without translation and scaling
            //We need to get the physical view port due to the fact that there can be circustances when zoom percentage
            //is very high, logical view port can be empty in such cases 
            GraphicsContainer graphicsState = graphics.BeginContainer();
            Matrix coOrdTxMatrix = new Matrix();
            coOrdTxMatrix.Scale(viewPortData.Scaling.Width, viewPortData.Scaling.Height, MatrixOrder.Prepend);
            coOrdTxMatrix.Invert();

            Point[] points = new Point[] { viewPortData.Translation, new Point(viewPortData.ViewPortSize) };
            coOrdTxMatrix.TransformPoints(points);
            Rectangle physicalViewPort = new Rectangle(points[0], new Size(points[1]));

            //because the watermark image needs to be scaled according to the zoom level, we
            //a) scale the graphics of the bitmap up by the zoom factor
            //a) scale the coordinates transform matrix down by the zoom factor
            coOrdTxMatrix = new Matrix();
            coOrdTxMatrix.Scale(viewPortData.Scaling.Width / (float)this.parentView.Zoom * 100.0f, viewPortData.Scaling.Height / (float)this.parentView.Zoom * 100.0f);

            //Make sure that we now clear the translation factor
            Matrix graphicsMatrics = new Matrix();
            graphicsMatrics.Scale((float)this.parentView.Zoom / 100.0f, (float)this.parentView.Zoom / 100.0f);
            graphics.Transform = graphicsMatrics;

            foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
            {
                //We do not draw the non intersecting pages, get the intersected viewport
                //We purposely use the physical viewport here because, there are cases in which the viewport
                //will not contain any logical bitmap areas in which case we atleast need to draw the pages properly
                if (!pageLayoutData.PageBounds.IntersectsWith(physicalViewPort))
                    continue;

                //Draw the watermark into the in-memory bitmap
                //This is the area of the viewport bitmap we need to copy on the page
                Rectangle viewPortBitmapArea = Rectangle.Empty;
                viewPortBitmapArea.X = pageLayoutData.LogicalPageBounds.X - viewPortData.LogicalViewPort.X;
                viewPortBitmapArea.Y = pageLayoutData.LogicalPageBounds.Y - viewPortData.LogicalViewPort.Y;
                viewPortBitmapArea.Width = pageLayoutData.LogicalPageBounds.Width;
                viewPortBitmapArea.Height = pageLayoutData.LogicalPageBounds.Height;

                //This rectangle is in translated logical units, we need to scale it down
                points = new Point[] { viewPortBitmapArea.Location, new Point(viewPortBitmapArea.Size) };
                coOrdTxMatrix.TransformPoints(points);
                viewPortBitmapArea.Location = points[0];
                viewPortBitmapArea.Size = new Size(points[1]);

                ActivityDesignerPaint.DrawImage(graphics, ambientTheme.WorkflowWatermarkImage, viewPortBitmapArea, new Rectangle(Point.Empty, ambientTheme.WorkflowWatermarkImage.Size), ambientTheme.WatermarkAlignment, AmbientTheme.WatermarkTransparency, false);
            }

            //Now clear the matrix
            graphics.EndContainer(graphicsState);
        }

        public override void OnPaintWorkflow(PaintEventArgs e, ViewPortData viewPortData)
        {
            Graphics graphics = e.Graphics;
            Debug.Assert(graphics != null);
            Bitmap memoryBitmap = viewPortData.MemoryBitmap;
            Debug.Assert(memoryBitmap != null);

            //Get the drawing canvas
            AmbientTheme ambientTheme = WorkflowTheme.CurrentTheme.AmbientTheme;

            //We set the highest quality interpolation so that we do not loose the image quality
            GraphicsContainer graphicsState = graphics.BeginContainer();

            //Fill the background using the workspace color so that we communicate the paging concept
            Rectangle workspaceRectangle = new Rectangle(Point.Empty, memoryBitmap.Size);
            graphics.FillRectangle(AmbientTheme.WorkspaceBackgroundBrush, workspaceRectangle);

            using (Font headerFooterFont = new Font(ambientTheme.Font.FontFamily, ambientTheme.Font.Size / this.scaling, ambientTheme.Font.Style))
            {
                int currentPage = 0;
                Matrix emptyMatrix = new Matrix();

                //Create the transformation matrix and calculate the physical viewport without translation and scaling
                //We need to get the physical view port due to the fact that there can be circustances when zoom percentage
                //is very high, logical view port can be empty in such cases 
                Matrix coOrdTxMatrix = new Matrix();
                coOrdTxMatrix.Scale(viewPortData.Scaling.Width, viewPortData.Scaling.Height, MatrixOrder.Prepend);
                coOrdTxMatrix.Invert();
                Point[] points = new Point[] { viewPortData.Translation, new Point(viewPortData.ViewPortSize) };
                coOrdTxMatrix.TransformPoints(points);
                coOrdTxMatrix.Invert();
                Rectangle physicalViewPort = new Rectangle(points[0], new Size(points[1]));

                //Create the data for rendering header/footer
                WorkflowPrintDocument.HeaderFooterData headerFooterData = new WorkflowPrintDocument.HeaderFooterData();
                headerFooterData.HeaderFooterMargins = this.headerFooterMargins;
                headerFooterData.PrintTime = this.previewTime;
                headerFooterData.TotalPages = this.pageLayoutInfo.Count;
                headerFooterData.Scaling = this.scaling;
                headerFooterData.Font = headerFooterFont;
                WorkflowDesignerLoader serviceDesignerLoader = this.serviceProvider.GetService(typeof(WorkflowDesignerLoader)) as WorkflowDesignerLoader;
                headerFooterData.FileName = (serviceDesignerLoader != null) ? serviceDesignerLoader.FileName : String.Empty;

                //Create the viewport transformation matrix
                Matrix viewPortMatrix = new Matrix();
                viewPortMatrix.Scale(viewPortData.Scaling.Width, viewPortData.Scaling.Height, MatrixOrder.Prepend);
                viewPortMatrix.Translate(-viewPortData.Translation.X, -viewPortData.Translation.Y, MatrixOrder.Append);

                //We now have the viewport properly drawn, now we need to draw it on the actual graphics object
                //Now that we have the designer bitmap we start splicing it based on the pages
                //Note that this is quite expensive operation and hence one should try to use
                //The memory bitmap we have got is scaled appropriately
                foreach (PageLayoutData pageLayoutData in this.pageLayoutInfo)
                {
                    currentPage += 1;

                    //We do not draw the non intersecting pages, get the intersected viewport
                    //We purposely use the physical viewport here because, there are cases in which the viewport
                    //will not contain any logical bitmap areas in which case we atleast need to draw the pages properly
                    if (!pageLayoutData.PageBounds.IntersectsWith(physicalViewPort) || pageLayoutData.PageBounds.Width <= 0 || pageLayoutData.PageBounds.Height <= 0)
                        continue;

                    //******START PAGE DRAWING, FIRST DRAW THE OUTLINE
                    //Scale and translate so that we can draw the pages
                    graphics.Transform = viewPortMatrix;
                    graphics.FillRectangle(Brushes.White, pageLayoutData.PageBounds);
                    ActivityDesignerPaint.DrawDropShadow(graphics, pageLayoutData.PageBounds, Color.Black, AmbientTheme.DropShadowWidth, LightSourcePosition.Left | LightSourcePosition.Top, 0.2f, false);

                    //***START BITMAP SPLICING
                    //Draw spliced bitmap for the page if we have any displayable area
                    Rectangle intersectedViewPort = pageLayoutData.LogicalPageBounds;
                    intersectedViewPort.Intersect(viewPortData.LogicalViewPort);
                    if (!intersectedViewPort.IsEmpty)
                    {
                        //Make sure that we now clear the translation factor
                        graphics.Transform = emptyMatrix;
                        //Paint bitmap on the pages
                        //Now that the page rectangle is actually drawn, we will scale down the Location of page rectangle
                        //so that we can draw the viewport bitmap part on it
                        Point bitmapDrawingPoint = Point.Empty;
                        bitmapDrawingPoint.X = pageLayoutData.ViewablePageBounds.X + Math.Abs(pageLayoutData.LogicalPageBounds.X - intersectedViewPort.X);
                        bitmapDrawingPoint.Y = pageLayoutData.ViewablePageBounds.Y + Math.Abs(pageLayoutData.LogicalPageBounds.Y - intersectedViewPort.Y);
                        points = new Point[] { bitmapDrawingPoint };
                        coOrdTxMatrix.TransformPoints(points);
                        bitmapDrawingPoint = new Point(points[0].X - viewPortData.Translation.X, points[0].Y - viewPortData.Translation.Y);

                        //This is the area of the viewport bitmap we need to copy on the page
                        Rectangle viewPortBitmapArea = Rectangle.Empty;
                        viewPortBitmapArea.X = intersectedViewPort.X - viewPortData.LogicalViewPort.X;
                        viewPortBitmapArea.Y = intersectedViewPort.Y - viewPortData.LogicalViewPort.Y;
                        viewPortBitmapArea.Width = intersectedViewPort.Width;
                        viewPortBitmapArea.Height = intersectedViewPort.Height;

                        //This rectangle is in translated logical units, we need to scale it down
                        points = new Point[] { viewPortBitmapArea.Location, new Point(viewPortBitmapArea.Size) };
                        coOrdTxMatrix.TransformPoints(points);
                        viewPortBitmapArea.Location = points[0];
                        viewPortBitmapArea.Size = new Size(points[1]);

                        ActivityDesignerPaint.DrawImage(graphics, memoryBitmap, new Rectangle(bitmapDrawingPoint, viewPortBitmapArea.Size), viewPortBitmapArea, DesignerContentAlignment.Fill, 1.0f, WorkflowTheme.CurrentTheme.AmbientTheme.DrawGrayscale);
                    }
                    //***END BITMAP SPLICING

                    //Draw the page outline
                    graphics.Transform = viewPortMatrix;
                    graphics.DrawRectangle(Pens.Black, pageLayoutData.PageBounds);

                    //Draw the printable page outline
                    graphics.DrawRectangle(ambientTheme.ForegroundPen, pageLayoutData.ViewablePageBounds.Left - 3, pageLayoutData.ViewablePageBounds.Top - 3, pageLayoutData.ViewablePageBounds.Width + 6, pageLayoutData.ViewablePageBounds.Height + 6);

                    //Draw the header and footer after we draw the actual page
                    headerFooterData.PageBounds = pageLayoutData.PageBounds;
                    headerFooterData.PageBoundsWithoutMargin = pageLayoutData.ViewablePageBounds;
                    headerFooterData.CurrentPage = currentPage;

                    //Draw the header
                    if (this.printDocument.PageSetupData.HeaderTemplate.Length > 0)
                        this.printDocument.PrintHeaderFooter(graphics, true, headerFooterData);

                    //Draw footer
                    if (this.printDocument.PageSetupData.FooterTemplate.Length > 0)
                        this.printDocument.PrintHeaderFooter(graphics, false, headerFooterData);
                    //***END DRAWING HEADER FOOTER
                }

                graphics.EndContainer(graphicsState);
            }
        }

        public override void Update(Graphics graphics, LayoutUpdateReason reason)
        {
            //do not recalculate pages when it's just a zoom change
            if (reason == LayoutUpdateReason.ZoomChanged)
                return;

            if (graphics == null)
                throw new ArgumentException("graphics");

            //Set the scaling, pageSize, margins, pageseparator by reserse scaling; so that when we actually scale
            //at the time of drawing things will be correctly calculated
            Size margin = WorkflowTheme.CurrentTheme.AmbientTheme.Margin;
            Size paperSize = GetPaperSize(graphics);
            Margins margins = GetAdjustedMargins(graphics);
            Size rootDesignerSize = (this.parentView.RootDesigner != null) ? this.parentView.RootDesigner.Size : Size.Empty;
            if (!rootDesignerSize.IsEmpty)
            {
                Size selectionSize = WorkflowTheme.CurrentTheme.AmbientTheme.SelectionSize;
                rootDesignerSize.Width += 3 * selectionSize.Width;
                rootDesignerSize.Height += 3 * selectionSize.Height;
            }

            //STEP1 : Calculate the scaling factor
            if (this.printDocument.PageSetupData.AdjustToScaleFactor)
            {
                this.scaling = ((float)this.printDocument.PageSetupData.ScaleFactor / 100.0f);
            }
            else
            {
                Size printableArea = new Size(paperSize.Width - (margins.Left + margins.Right), paperSize.Height - (margins.Top + margins.Bottom));
                printableArea.Width = Math.Max(printableArea.Width, 1);
                printableArea.Height = Math.Max(printableArea.Height, 1);

                PointF scaleFactor = new PointF(
                ((float)this.printDocument.PageSetupData.PagesWide * (float)printableArea.Width / (float)rootDesignerSize.Width),
                ((float)this.printDocument.PageSetupData.PagesTall * (float)printableArea.Height / (float)rootDesignerSize.Height));

                //Take the minimum scaling as we do not want to unevenly scale the bitmap
                this.scaling = Math.Min(scaleFactor.X, scaleFactor.Y);
                //leave just 3 digital points (also, that will remove potential problems with ceiling e.g. when the number of pages would be 3.00000000001 we'll get 4)
                this.scaling = (float)(Math.Floor((double)this.scaling * 1000.0d) / 1000.0d);
            }

            //STEP2 : Calculate the pagesize
            this.pageSize = paperSize;
            this.pageSize.Width = Convert.ToInt32(Math.Ceiling(((float)this.pageSize.Width) / this.scaling));
            this.pageSize.Height = Convert.ToInt32(Math.Ceiling(((float)this.pageSize.Height) / this.scaling));

            //STEP3 : Calculate the page separator
            IDesignerOptionService designerOptionService = this.serviceProvider.GetService(typeof(IDesignerOptionService)) as IDesignerOptionService;
            if (designerOptionService != null)
            {
                object separator = designerOptionService.GetOptionValue("WinOEDesigner", "PageSeparator");
                PageSeparator = (separator != null) ? (Size)separator : PrintPreviewLayout.DefaultPageSeparator;
            }
            PageSeparator = new Size(Convert.ToInt32(Math.Ceiling(((float)PageSeparator.Width) / this.scaling)), Convert.ToInt32(Math.Ceiling(((float)PageSeparator.Height) / this.scaling)));

            //STEP4: Calculate the margins after reverse scaling the margins, so that when we set the normal scalezoom we have correct margins
            PageMargins = margins;
            PageMargins.Left = Convert.ToInt32((float)PageMargins.Left / this.scaling);
            PageMargins.Right = Convert.ToInt32((float)PageMargins.Right / this.scaling);
            PageMargins.Top = Convert.ToInt32((float)PageMargins.Top / this.scaling);
            PageMargins.Bottom = Convert.ToInt32((float)PageMargins.Bottom / this.scaling);

            //STEP5: Calculate the header and footer margins
            this.headerFooterMargins.Top = Convert.ToInt32((float)this.printDocument.PageSetupData.HeaderMargin / this.scaling);
            this.headerFooterMargins.Bottom = Convert.ToInt32((float)this.printDocument.PageSetupData.FooterMargin / this.scaling);
            this.previewTime = DateTime.Now;

            //STEP6: Calculate the the row columns
            Size viewablePageSize = new Size(this.pageSize.Width - (PageMargins.Left + PageMargins.Right), this.pageSize.Height - (PageMargins.Top + PageMargins.Bottom));
            viewablePageSize.Width = Math.Max(viewablePageSize.Width, 1);
            viewablePageSize.Height = Math.Max(viewablePageSize.Height, 1);

            //We check for greater than 1 here as the division might introduce rounding factor
            //Columns
            this.rowColumns.Width = rootDesignerSize.Width / viewablePageSize.Width;
            this.rowColumns.Width += ((rootDesignerSize.Width % viewablePageSize.Width) > 1) ? 1 : 0;
            this.rowColumns.Width = Math.Max(1, this.rowColumns.Width);

            //Rows
            this.rowColumns.Height = rootDesignerSize.Height / viewablePageSize.Height;
            this.rowColumns.Height += ((rootDesignerSize.Height % viewablePageSize.Height) > 1) ? 1 : 0;
            this.rowColumns.Height = Math.Max(1, this.rowColumns.Height);

            //STEP7: Calculate the pagelayoutdata
            this.pageLayoutInfo.Clear();

            //Create the layout data
            for (int row = 0; row < this.rowColumns.Height; row++)
            {
                for (int column = 0; column < this.rowColumns.Width; column++)
                {
                    Point pageLocation = Point.Empty;
                    pageLocation.X = (column * this.pageSize.Width) + ((column + 1) * PageSeparator.Width);
                    pageLocation.Y = (row * this.pageSize.Height) + ((row + 1) * PageSeparator.Height);

                    Point viewablePageLocation = Point.Empty;
                    viewablePageLocation.X = pageLocation.X + PageMargins.Left;
                    viewablePageLocation.Y = pageLocation.Y + PageMargins.Top;

                    Rectangle logicalBounds = new Rectangle(column * viewablePageSize.Width, row * viewablePageSize.Height, viewablePageSize.Width, viewablePageSize.Height);
                    Rectangle pageBounds = new Rectangle(pageLocation, this.pageSize);
                    Rectangle viewablePageBounds = new Rectangle(viewablePageLocation, viewablePageSize);
                    this.pageLayoutInfo.Add(new PageLayoutData(logicalBounds, pageBounds, viewablePageBounds, new Point(column, row)));
                }
            }
        }
        #endregion

        #region Helpers
        private Size GetPaperSize(Graphics graphics)
        {
            Size size = Size.Empty;
            PaperSize paperSize = this.printDocument.DefaultPageSettings.PaperSize;
            this.printDocument.DefaultPageSettings.PaperSize = paperSize;
            if (this.printDocument.PageSetupData.Landscape)
            {
                size.Width = Math.Max(paperSize.Height, 1);
                size.Height = Math.Max(paperSize.Width, 1);
            }
            else
            {
                size.Width = Math.Max(paperSize.Width, 1);
                size.Height = Math.Max(paperSize.Height, 1);
            }

            return size;
        }

        private Margins GetAdjustedMargins(Graphics graphics)
        {
            Margins margins = this.printDocument.PageSetupData.Margins;
            if (this.printDocument.PageSetupData.Landscape)
            {
                int temp = margins.Left;
                margins.Left = margins.Right;
                margins.Right = temp;

                temp = margins.Bottom;
                margins.Bottom = margins.Top;
                margins.Top = temp;
            }

            //Read the unprintable margins
            Margins hardMargins = new Margins();
            using (Graphics printerGraphics = this.printDocument.PrinterSettings.CreateMeasurementGraphics())
                hardMargins = this.printDocument.GetHardMargins(printerGraphics);

            Margins adjustedMargins = new Margins(Math.Max(margins.Left, hardMargins.Left),
                                                  Math.Max(margins.Right, hardMargins.Right),
                                                  Math.Max(margins.Top, hardMargins.Top),
                                                  Math.Max(margins.Bottom, hardMargins.Bottom));

            return adjustedMargins;
        }

        private Size PageSeparator
        {
            get
            {
                return this.pageSeparator;
            }
            set
            {
                this.pageSeparator = value;
            }
        }

        private Margins PageMargins
        {
            get
            {
                return this.pageMargins;
            }

            set
            {
                this.pageMargins = value;
            }
        }
        #endregion

        #region Struct PageLayoutData
        // Please note that all the page bounds in here are in scaled coordinates
        // PageBounds are the bounds of the page in scaled coordinates with margins
        // ViewablePageBounds are the bounds in scaled coordinates without margins
        // LogicalBounds are the page bounds when mapped to our logical coordinate system.
        //             |-----------------------|
        //             |PageBounds             |
        //             |   |---------------|...........Mapped to logical page bounds
        //             |   |Viewable       |   |
        //             |   |PageBounds     |   |
        //             |   |               |   |
        //             |   |               |   |
        //             |   |               |   |
        //             |   |               |   |
        //             |   |               |   |
        //             |   |               |   |
        //             |   |               |   |
        //             |   |---------------|...........
        //             |                       |
        //             -------------------------
        private struct PageLayoutData
        {
            //logical page bounds start from 0,0 and go to the size of the designer
            public Rectangle LogicalPageBounds;
            //Page bounds are used to draw the complete page in the layout with margin
            public Rectangle PageBounds;
            //screen viewable page bounds (Excludes the margin area)
            public Rectangle ViewablePageBounds;
            //row column position contains the row column position of the page
            public Point Position;

            public PageLayoutData(Rectangle logicalPageBounds, Rectangle pageBounds, Rectangle viewablePageBounds, Point rowColumnPos)
            {
                this.LogicalPageBounds = logicalPageBounds;
                this.PageBounds = pageBounds;

                //Exclude the margins themselves 
                this.ViewablePageBounds = viewablePageBounds;
                this.Position = rowColumnPos;
            }
        }
        #endregion
    }
    #endregion
}

