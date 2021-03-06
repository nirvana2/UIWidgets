using System;
using System.Collections.Generic;
using Unity.UIWidgets.animation;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.gestures;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.rendering;
using Unity.UIWidgets.ui;
using Unity.UIWidgets.widgets;

namespace Unity.UIWidgets.material {
    public enum DrawerAlignment {
        start,
        end
    }

    public class Drawer : StatelessWidget {
        public Drawer(
            Key key = null,
            double elevation = 16.0,
            Widget child = null) : base(key: key) {
            this.elevation = elevation;
            this.child = child;
        }

        public readonly double elevation;

        public readonly Widget child;

        public override Widget build(BuildContext context) {
            return new ConstrainedBox(
                constraints: BoxConstraints.expand(width: DrawerUtils._kWidth),
                child: new Material(
                    elevation: this.elevation,
                    child: this.child
                )
            );
        }
    }

    public delegate void DrawerCallback(bool isOpened);


    public class DrawerController : StatefulWidget {
        public DrawerController(
            GlobalKey key = null,
            Widget child = null,
            DrawerAlignment? alignment = null,
            DrawerCallback drawerCallback = null) : base(key: key) {
            D.assert(child != null);
            D.assert(alignment != null);
            this.child = child;
            this.alignment = alignment ?? DrawerAlignment.start;
            this.drawerCallback = drawerCallback;
        }

        public readonly Widget child;

        public readonly DrawerAlignment alignment;

        public readonly DrawerCallback drawerCallback;

        public override State createState() {
            return new DrawerControllerState();
        }
    }


    public class DrawerControllerState : SingleTickerProviderStateMixin<DrawerController> {
        public override void initState() {
            base.initState();
            this._controller = new AnimationController(duration: DrawerUtils._kBaseSettleDuration, vsync: this);
            this._controller.addListener(this._animationChanged);
            this._controller.addStatusListener(this._animationStatusChanged);
        }

        public override void dispose() {
            this._historyEntry?.remove();
            this._controller.dispose();
            base.dispose();
        }

        void _animationChanged() {
            this.setState(() => { });
        }

        LocalHistoryEntry _historyEntry;
        readonly FocusScopeNode _focusScopeNode = new FocusScopeNode();

        void _ensureHistoryEntry() {
            if (this._historyEntry == null) {
                ModalRoute route = ModalRoute.of(this.context);
                if (route != null) {
                    this._historyEntry = new LocalHistoryEntry(onRemove: this._handleHistoryEntryRemoved);
                    route.addLocalHistoryEntry(this._historyEntry);
                    FocusScope.of(this.context).setFirstFocus(this._focusScopeNode);
                }
            }
        }

        void _animationStatusChanged(AnimationStatus status) {
            switch (status) {
                case AnimationStatus.forward:
                    this._ensureHistoryEntry();
                    break;
                case AnimationStatus.reverse:
                    this._historyEntry?.remove();
                    this._historyEntry = null;
                    break;
                case AnimationStatus.dismissed:
                    break;
                case AnimationStatus.completed:
                    break;
            }
        }

        void _handleHistoryEntryRemoved() {
            this._historyEntry = null;
            this.close();
        }

        AnimationController _controller;


        void _handleDragDown(DragDownDetails details) {
            this._controller.stop();
            this._ensureHistoryEntry();
        }

        void _handleDragCancel() {
            if (this._controller.isDismissed || this._controller.isAnimating) {
                return;
            }

            if (this._controller.value < 0.5) {
                this.close();
            }
            else {
                this.open();
            }
        }

        public readonly GlobalKey _drawerKey = GlobalKey.key();


        double _width {
            get {
                RenderBox box = (RenderBox) this._drawerKey.currentContext?.findRenderObject();
                if (box != null) {
                    return box.size.width;
                }

                return DrawerUtils._kWidth;
            }
        }

        bool _previouslyOpened = false;

        void _move(DragUpdateDetails details) {
            double delta = (details.primaryDelta ?? 0) / this._width;
            switch (this.widget.alignment) {
                case DrawerAlignment.start:
                    break;
                case DrawerAlignment.end:
                    delta = -delta;
                    break;
            }

            this._controller.setValue(this._controller.value + delta);

            bool opened = this._controller.value > 0.5;
            if (opened != this._previouslyOpened && this.widget.drawerCallback != null) {
                this.widget.drawerCallback(opened);
            }

            this._previouslyOpened = opened;
        }

        void _settle(DragEndDetails details) {
            if (this._controller.isDismissed) {
                return;
            }

            if (details.velocity.pixelsPerSecond.dx.abs() >= DrawerUtils._kMinFlingVelocity) {
                double visualVelocity = details.velocity.pixelsPerSecond.dx / DrawerUtils._kWidth;
                switch (this.widget.alignment) {
                    case DrawerAlignment.start:
                        break;
                    case DrawerAlignment.end:
                        visualVelocity = -visualVelocity;
                        break;
                }

                this._controller.fling(velocity: visualVelocity);
            }
            else if (this._controller.value < 0.5) {
                this.close();
            }
            else {
                this.open();
            }
        }

        public void open() {
            this._controller.fling(velocity: 1.0);
            if (this.widget.drawerCallback != null) {
                this.widget.drawerCallback(true);
            }
        }

        public void close() {
            this._controller.fling(velocity: -1.0);
            if (this.widget.drawerCallback != null) {
                this.widget.drawerCallback(false);
            }
        }

        ColorTween _color = new ColorTween(begin: Colors.transparent, end: Colors.black54);
        GlobalKey _gestureDetectorKey = GlobalKey.key();

        Alignment _drawerOuterAlignment {
            get {
                switch (this.widget.alignment) {
                    case DrawerAlignment.start:
                        return Alignment.centerLeft;
                    case DrawerAlignment.end:
                        return Alignment.centerRight;
                }

                return null;
            }
        }

        Alignment _drawerInnerAlignment {
            get {
                switch (this.widget.alignment) {
                    case DrawerAlignment.start:
                        return Alignment.centerRight;
                    case DrawerAlignment.end:
                        return Alignment.centerLeft;
                }

                return null;
            }
        }

        Widget _buildDrawer(BuildContext context) {
            bool drawerIsStart = this.widget.alignment == DrawerAlignment.start;
            EdgeInsets padding = MediaQuery.of(context).padding;
            double dragAreaWidth = drawerIsStart ? padding.left : padding.right;

            dragAreaWidth = Math.Max(dragAreaWidth, DrawerUtils._kEdgeDragWidth);
            if (this._controller.status == AnimationStatus.dismissed) {
                return new Align(
                    alignment: this._drawerOuterAlignment,
                    child: new GestureDetector(
                        key: this._gestureDetectorKey,
                        onHorizontalDragUpdate: this._move,
                        onHorizontalDragEnd: this._settle,
                        behavior: HitTestBehavior.translucent,
                        child: new Container(width: dragAreaWidth)
                    )
                );
            }
            else {
                return new GestureDetector(
                    key: this._gestureDetectorKey,
                    onHorizontalDragDown: this._handleDragDown,
                    onHorizontalDragUpdate: this._move,
                    onHorizontalDragEnd: this._settle,
                    onHorizontalDragCancel: this._handleDragCancel,
                    child: new RepaintBoundary(
                        child: new Stack(
                            children: new List<Widget> {
                                new GestureDetector(
                                    onTap: this.close,
                                    child: new Container(
                                        color: this._color.evaluate(this._controller)
                                    )
                                ),
                                new Align(
                                    alignment: this._drawerOuterAlignment,
                                    child: new Align(
                                        alignment: this._drawerInnerAlignment,
                                        widthFactor: this._controller.value,
                                        child: new RepaintBoundary(
                                            child: new FocusScope(
                                                key: this._drawerKey,
                                                node: this._focusScopeNode,
                                                child: this.widget.child)
                                        )
                                    )
                                )
                            }
                        )
                    )
                );
            }
        }


        public override Widget build(BuildContext context) {
            return new ListTileTheme(
                style: ListTileStyle.drawer,
                child: this._buildDrawer(context));
        }
    }
}