﻿using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace UnityEditor.ShaderGraph.Drawing
{
    enum ResizeDirection
    {
        Any,
        Vertical,
        Horizontal
    }

    public enum ResizeHandleAnchor
    {
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
        TopLeft
    }

    public class ResizeSideHandle : VisualElement
    {
        VisualElement m_ResizeTarget;

        bool m_StayWithinParentBounds;

        public bool stayWithinParentBounds
        {
            get { return m_StayWithinParentBounds; }
            set { m_StayWithinParentBounds = value; }
        }

        bool m_MaintainAspectRatio;

        public bool maintainAspectRatio
        {
            get { return m_MaintainAspectRatio; }
            set { m_MaintainAspectRatio = value; }
        }

        public Action OnResizeFinished;

        bool m_DockingLeft;
        bool m_DockingTop;

        public ResizeSideHandle(VisualElement resizeTarget, ResizeHandleAnchor anchor)
        {
            m_ResizeTarget = resizeTarget;

            AddToClassList("resize");

            switch (anchor)
            {
                case ResizeHandleAnchor.Top:
                {
                    AddToClassList("vertical");
                    AddToClassList("top");
                    break;
                }
                case ResizeHandleAnchor.TopRight:
                {
                    AddToClassList("diagonal");
                    AddToClassList("top-right");
                    break;
                }
                case ResizeHandleAnchor.Right:
                {
                    AddToClassList("horizontal");
                    AddToClassList("right");
                    break;
                }
                case ResizeHandleAnchor.BottomRight:
                {
                    AddToClassList("diagonal");
                    AddToClassList("bottom-right");
                    break;
                }
                case ResizeHandleAnchor.Bottom:
                {
                    AddToClassList("vertical");
                    AddToClassList("bottom");
                    break;
                }
                case ResizeHandleAnchor.BottomLeft:
                {
                    AddToClassList("diagonal");
                    AddToClassList("bottom-left");
                    break;
                }
                case ResizeHandleAnchor.Left:
                {
                    AddToClassList("horizontal");
                    AddToClassList("left");
                    break;
                }
                case ResizeHandleAnchor.TopLeft:
                {
                    AddToClassList("diagonal");
                    AddToClassList("top-left");
                    break;
                }
            }

            if (anchor == ResizeHandleAnchor.Left || anchor == ResizeHandleAnchor.Right)
            {
                this.AddManipulator(new Draggable(mouseDelta => OnResizeFromHorizontal(mouseDelta, anchor)));
            }
            else if (anchor == ResizeHandleAnchor.Top || anchor == ResizeHandleAnchor.Bottom)
            {
                this.AddManipulator(new Draggable(mouseDelta => OnResizeFromVertical(mouseDelta, anchor)));
            }
            else
            {
                this.AddManipulator(new Draggable(mouseDelta => OnResizeFromCorner(mouseDelta, anchor)));
            }

            RegisterCallback<MouseDownEvent>(HandleMouseDown);
            RegisterCallback<MouseUpEvent>(HandleDraggableMouseUp);
        }

        static bool IsAnchorLeft(ResizeHandleAnchor anchor)
        {
            return anchor == ResizeHandleAnchor.Left || anchor == ResizeHandleAnchor.TopLeft || anchor == ResizeHandleAnchor.BottomLeft;
        }

        static bool IsAnchorTop(ResizeHandleAnchor anchor)
        {
            return anchor == ResizeHandleAnchor.Top || anchor == ResizeHandleAnchor.TopLeft || anchor == ResizeHandleAnchor.TopRight;
        }

        static bool IsAnchorCorner(ResizeHandleAnchor anchor)
        {
            return anchor == ResizeHandleAnchor.TopLeft ||
                anchor == ResizeHandleAnchor.TopRight ||
                anchor == ResizeHandleAnchor.BottomLeft ||
                anchor == ResizeHandleAnchor.BottomRight;
        }

        Vector2 GetMinSize()
        {
            Vector2 minSize = new Vector2(60f, 60f);

            if (!Mathf.Approximately(m_ResizeTarget.style.minWidth.value, 0f))
            {
                minSize.x = m_ResizeTarget.style.minWidth;
            }

            if (!Mathf.Approximately(m_ResizeTarget.style.minHeight.value, 0f))
            {
                minSize.y = m_ResizeTarget.style.minHeight.value;
            }

            return minSize;
        }

        float GetMaxHorizontalExpansion(bool expandingLeft)
        {
            float maxHorizontalExpansion;

            if (expandingLeft)
            {
                maxHorizontalExpansion = m_ResizeTarget.layout.x;
            }
            else
            {
                maxHorizontalExpansion =  m_ResizeTarget.parent.layout.width - m_ResizeTarget.layout.xMax;
            }

            if (maintainAspectRatio)
            {
                if (!m_DockingTop)
                {
                    maxHorizontalExpansion = Mathf.Min(maxHorizontalExpansion, m_ResizeTarget.layout.y);
                }
                else
                {
                    maxHorizontalExpansion = Mathf.Min(maxHorizontalExpansion, m_ResizeTarget.parent.layout.height - m_ResizeTarget.layout.yMax);
                }
            }

            return maxHorizontalExpansion;
        }

        float GetMaxVerticalExpansion(bool expandingUp)
        {
            float maxVerticalExpansion;

            if (expandingUp)
            {
                maxVerticalExpansion = m_ResizeTarget.layout.x;
            }
            else
            {
                maxVerticalExpansion =  m_ResizeTarget.parent.layout.width - m_ResizeTarget.layout.xMax;
            }

            if (maintainAspectRatio)
            {
                if (!m_DockingLeft)
                {
                    maxVerticalExpansion = Mathf.Min(maxVerticalExpansion, m_ResizeTarget.layout.x);
                }
                else
                {
                    maxVerticalExpansion = Mathf.Min(maxVerticalExpansion, m_ResizeTarget.parent.layout.width - m_ResizeTarget.layout.xMax);
                }
            }

            return maxVerticalExpansion;
        }

        float GetMaxContraction()
        {
            Vector2 minSize = GetMinSize();
            return Mathf.Min(m_ResizeTarget.layout.width - minSize.x, m_ResizeTarget.layout.height - minSize.y);
        }

        void OnResizeFromHorizontal(Vector2 resizeDelta, ResizeHandleAnchor anchor)
        {
            float resizeAmount = resizeDelta.x / 2f;
            float aspectRatio = m_ResizeTarget.layout.width / m_ResizeTarget.layout.height;
            Vector2 minSize = GetMinSize();

            Rect newLayout = m_ResizeTarget.layout;

            bool resizeLeft = anchor == ResizeHandleAnchor.Left;

            if (stayWithinParentBounds)
            {
                resizeAmount = Mathf.Clamp(resizeAmount, GetMaxContraction(), GetMaxHorizontalExpansion(resizeLeft));
            }

            if (resizeLeft)
            {
                newLayout.xMin += resizeAmount;
            }
            else
            {
                newLayout.xMax += resizeAmount;
            }

            if (maintainAspectRatio)
            {
                if (!resizeLeft)
                {
                    resizeAmount = -resizeAmount;
                }

                if (!m_DockingTop)
                {
                    newLayout.yMin += resizeAmount;
                }
                else
                {
                    newLayout.yMax -= resizeAmount;
                }
            }

            m_ResizeTarget.layout = newLayout;
        }

        void OnResizeFromVertical(Vector2 resizeDelta, ResizeHandleAnchor anchor)
        {
        }

        void OnResizeFromCorner(Vector2 resizeDelta, ResizeHandleAnchor anchor)
        {
            Vector2 normalizedResizeDelta = resizeDelta / 2f;
            Vector2 minSize = GetMinSize();
        }

        void OnResize(Vector2 resizeDelta, ResizeDirection direction, ResizeHandleAnchor anchor)
        {
            Vector2 normalizedResizeDelta = resizeDelta / 2f;

            Vector2 minSize = new Vector2(60f, 60f);

            if (!Mathf.Approximately(m_ResizeTarget.style.minWidth.value, 0f))
            {
                minSize.x = m_ResizeTarget.style.minWidth;
            }

            if (!Mathf.Approximately(m_ResizeTarget.style.minHeight.value, 0f))
            {
                minSize.y = m_ResizeTarget.style.minHeight.value;
            }

            if (direction == ResizeDirection.Vertical)
            {
                normalizedResizeDelta.x = 0f;
            }
            else if (direction == ResizeDirection.Horizontal)
            {
                normalizedResizeDelta.y = 0f;
            }

            Rect newLayout = m_ResizeTarget.layout;

            if (maintainAspectRatio)
            {
                bool dominantResizeAlongX = Mathf.Abs(normalizedResizeDelta.x) >= Mathf.Abs(normalizedResizeDelta.y);

                float aspectRatio = m_ResizeTarget.layout.width / m_ResizeTarget.layout.height;

                bool expanding;

                if (dominantResizeAlongX)
                {
                    expanding = (IsAnchorLeft(anchor) && normalizedResizeDelta.x < 0f) || (!IsAnchorLeft(anchor) && normalizedResizeDelta.x > 0f);
                }
                else
                {
                    expanding = (IsAnchorTop(anchor) && normalizedResizeDelta.y < 0f) || (!IsAnchorTop(anchor) && normalizedResizeDelta.y > 0f);
                }

                if (dominantResizeAlongX)
                {
                    normalizedResizeDelta.y = 0f;

                    float changeAlongY = Mathf.Abs(normalizedResizeDelta.x) / aspectRatio * .5f;

                    changeAlongY = expanding ? changeAlongY : -changeAlongY;

                    newLayout.yMin -= changeAlongY;
                    newLayout.yMax += changeAlongY;
                }
                else
                {
                    normalizedResizeDelta.x = 0f;
                    float changeAlongX = Mathf.Abs(normalizedResizeDelta.y) * aspectRatio * .5f;

                    changeAlongX = expanding ? changeAlongX : -changeAlongX;

                    newLayout.xMin -= changeAlongX;
                    newLayout.xMax += changeAlongX;
                }
            }

            if (IsAnchorLeft(anchor))
            {
                newLayout.xMin = Mathf.Min(newLayout.xMin + normalizedResizeDelta.x, newLayout.xMax - minSize.x);
            }
            else
            {
                newLayout.xMax = Mathf.Max(newLayout.xMax + normalizedResizeDelta.x, newLayout.xMin + minSize.x);
            }

            if (IsAnchorTop(anchor))
            {
                newLayout.yMin = Mathf.Min(newLayout.yMin + normalizedResizeDelta.y, newLayout.yMax - minSize.y);
            }
            else
            {
                newLayout.yMax = Mathf.Max(newLayout.yMax + normalizedResizeDelta.y, newLayout.yMin + minSize.y);
            }

            if (stayWithinParentBounds)
            {
                newLayout.xMin = Mathf.Max(newLayout.xMin, 0f);
                newLayout.yMin = Mathf.Max(newLayout.yMin, 0f);
                newLayout.xMax = Mathf.Min(newLayout.xMax, m_ResizeTarget.parent.layout.width);
                newLayout.yMax = Mathf.Min(newLayout.yMax, m_ResizeTarget.parent.layout.height);
            }

            m_ResizeTarget.layout = newLayout;
        }

        void HandleMouseDown(MouseDownEvent evt)
        {
            m_DockingLeft = m_ResizeTarget.layout.center.x / m_ResizeTarget.parent.layout.width < .5f;
            m_DockingTop = m_ResizeTarget.layout.center.y / m_ResizeTarget.parent.layout.height < .5f;

            Debug.Log("docking left: " + m_DockingLeft + " docking top: " + m_DockingTop);
        }

        void HandleDraggableMouseUp(MouseUpEvent evt)
        {
            if (OnResizeFinished != null)
            {
                OnResizeFinished();
            }
        }
    }
}
