﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Text;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        
        /// <summary>
        /// Floor center: coordinate X
        /// </summary>
        private const double FloorCenterX = 0.0;

        /// <summary>
        /// Floor center: coordinate Y
        /// </summary>
        private const double FloorCenterY = -1.0;

        /// <summary>
        /// Floor center: coordinate Z
        /// </summary>
        private const double FloorCenterZ = 2.5;

        /// <summary>
        /// User head height
        /// </summary>
        private double HeadHeight = -10;

        /// <summary>
        /// True if position has been recognized, false otherwise
        /// </summary>
        private bool PositionDone = false;

        /// <summary>
        /// True if gesture start is correct, false otherwise
        /// </summary>
        private bool GestureStartB = false;

        /// <summary>
        /// True if gesture end is correct, false otherwise
        /// </summary>
        private bool GestureEndB = false;

        /// <summary>
        /// Number of gestures done
        /// </summary>
        private int GesturesDone = 0;

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing reached goals
        /// </summary>
        private readonly Brush goalReachedBrush = new SolidColorBrush(Color.FromArgb(64, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing not reached goals
        /// </summary>
        private readonly Brush goalNotReachedBrush = new SolidColorBrush(Color.FromArgb(64, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing gesture line
        /// </summary>
        private readonly Brush gestureLineBrush = new SolidColorBrush(Color.FromArgb(150, 255, 215, 0));

        /// <summary>
        /// Brush used for drawing gesture points
        /// </summary>
        private readonly Brush gesturePointBrush = new SolidColorBrush(Color.FromArgb(150, 255, 165, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();


        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }
                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    int penIndex = 0;

                    // Flag to detect only one body
                    Boolean firstBody = false;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked && firstBody == false)
                        {
                            firstBody = true;

                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawBody(joints, jointPoints, dc, drawPen);

                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], joints[JointType.HandLeft].Position.Z, dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], joints[JointType.HandRight].Position.Z, dc);



                            if (HeadHeight == -10)
                            {
                                HeadHeight = this.DrawFloor(joints[JointType.FootLeft], joints[JointType.FootRight], joints[JointType.Head], dc);
                            }
                            else if (!PositionDone)
                            {
                                Joint left_goal = new Joint();
                                left_goal.Position.X = (float)FloorCenterX - 0.4f;
                                left_goal.Position.Y = (float)HeadHeight - 0.6f;
                                left_goal.Position.Z = (float)FloorCenterZ;
                                Joint right_goal = new Joint();
                                right_goal.Position.X = (float)FloorCenterX + 0.3f;
                                right_goal.Position.Y = (float)HeadHeight - 0.25f;
                                right_goal.Position.Z = (float)FloorCenterZ - 0.4f;
                                bool leftGoalB = this.DrawGoal(left_goal, joints[JointType.HandLeft], dc);
                                bool rightGoalB = this.DrawGoal(right_goal, joints[JointType.HandRight], dc);

                                ////////////////////////////////////////////
                                PositionDone = leftGoalB && rightGoalB;

                                System.String imgPath = Path.GetFullPath(@"..\..\..\Images\good.png");
                                position.Source = new BitmapImage(new Uri(imgPath));

                            }
                            //If the position is detected, start gesture recognition:
                            else
                            {
                                System.String imgPath = Path.GetFullPath(@"..\..\..\Images\good.png");
                                posture.Source = new BitmapImage(new Uri(imgPath));

                                //Define gesture start point:
                                Joint gesture_start = new Joint();
                                gesture_start.Position.X = (float)FloorCenterX + 0.3f;
                                gesture_start.Position.Y = (float)HeadHeight - 0.25f;
                                gesture_start.Position.Z = (float)FloorCenterZ - 0.4f;
                                //Define the gesture with new goal:
                                Joint gesture_end = new Joint();
                                gesture_end.Position.X = (float)FloorCenterX - 0.2f;
                                gesture_end.Position.Y = (float)HeadHeight - 0.25f;
                                gesture_end.Position.Z = (float)FloorCenterZ - 0.4f;

                                if (!GestureStartB)
                                {
                                    GestureStartB = this.DrawGesturePoint(gesture_start, joints[JointType.HandRight], dc);
                                }
                                //We have to reach the new goal without going out of the limits (up and down)
                                else if (
                                    joints[JointType.HandRight].Position.Y > gesture_start.Position.Y + 0.2 ||
                                    joints[JointType.HandRight].Position.Y < gesture_start.Position.Y - 0.2 ||
                                    joints[JointType.HandRight].Position.X > gesture_start.Position.X + 0.1)
                                {
                                    GestureStartB = false;
                                }
                                else
                                {
                                    GestureEndB = this.DrawGesturePoint(gesture_end, joints[JointType.HandRight], dc);
                                    //Draw the line to help the user:
                                    DepthSpacePoint gesture_start_depth = this.coordinateMapper.MapCameraPointToDepthSpace(joints[JointType.HandRight].Position);
                                    Point gesture_start_2D = new Point(gesture_start_depth.X, gesture_start_depth.Y);
                                    DepthSpacePoint gesture_end_depth = this.coordinateMapper.MapCameraPointToDepthSpace(gesture_end.Position);
                                    Point gesture_end_2D = new Point(gesture_end_depth.X, gesture_end_depth.Y);
                                    Pen line_pen = new Pen(gestureLineBrush, 10);
                                    dc.DrawLine(line_pen, gesture_start_2D, gesture_end_2D);
                                    //If we reach the second goal without moving out, add +1 and reset
                                    if (GestureEndB)
                                    {
                                        GesturesDone++;
                                        gestureText.Text = Convert.ToString(GesturesDone);
                                        GestureStartB = false;
                                        GestureEndB = false;
                                    }
                                }

                            }

                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, float z, DrawingContext drawingContext)
        {
            double ellipseSize = 2.0 / z;
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, ellipseSize * HandSize, ellipseSize * HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, ellipseSize * HandSize, ellipseSize * HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, ellipseSize * HandSize, ellipseSize * HandSize);
                    break;
            }
        }



        /// <summary>
        /// Draws an ellipse in the posture goal
        /// </summary>
        /// <param name="goal">position of the hand</param>
        /// <param name="hand">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private bool DrawGoal(Joint goal, Joint hand, DrawingContext drawingContext)
        {
            // New factor to give deep feeling being bigger or closer
            double ellipseSize = 2.0 / goal.Position.Z;
            // Transform world coordinates to screen coordinates
            DepthSpacePoint depth_goal = this.coordinateMapper.MapCameraPointToDepthSpace(goal.Position);
            Point goal_2D = new Point(depth_goal.X, depth_goal.Y);

            // If the hand is inside of the goal, the ellipse change it color to red
            if ((hand.Position.X < goal.Position.X + 0.05 && hand.Position.X > goal.Position.X - 0.05) &&
                (hand.Position.Y < goal.Position.Y + 0.05 && hand.Position.Y > goal.Position.Y - 0.05) &&
                (hand.Position.Z < goal.Position.Z + 0.05 && hand.Position.Z > goal.Position.Z - 0.05))
            {
                drawingContext.DrawEllipse(this.goalReachedBrush, null, goal_2D, ellipseSize * HandSize, ellipseSize * HandSize);
                return true;
            }
            // If not, the ellipse change it color to red
            else
            {
                drawingContext.DrawEllipse(this.goalNotReachedBrush, null, goal_2D, ellipseSize * HandSize, ellipseSize * HandSize);
            }
            return false;
        }

        /// <summary>
        /// Draws an ellipse in the gesture goal
        /// </summary>
        /// <param name="goal">position of the hand</param>
        /// <param name="hand">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private bool DrawGesturePoint(Joint goal, Joint hand, DrawingContext drawingContext)
        {
            // New factor to give deep feeling being bigger or closer
            double ellipseSize = 2.0 / goal.Position.Z;
            // Transform world coordinates to screen coordinates
            DepthSpacePoint depth_goal = this.coordinateMapper.MapCameraPointToDepthSpace(goal.Position);
            Point goal_2D = new Point(depth_goal.X, depth_goal.Y);

            // If the hand is inside of the goal, return true
            if ((hand.Position.X < goal.Position.X + 0.05 && hand.Position.X > goal.Position.X - 0.05) &&
                (hand.Position.Y < goal.Position.Y + 0.05 && hand.Position.Y > goal.Position.Y - 0.05) &&
                (hand.Position.Z < goal.Position.Z + 0.05 && hand.Position.Z > goal.Position.Z - 0.05))
            {
                drawingContext.DrawEllipse(this.gesturePointBrush, null, goal_2D, ellipseSize * HandSize, ellipseSize * HandSize);
                return true;
            }
            // If not, return false
            else
            {
                drawingContext.DrawEllipse(this.gesturePointBrush, null, goal_2D, ellipseSize * HandSize, ellipseSize * HandSize);
            }
            return false;
        }


        /// <summary>
        /// Draws an ellipse in the floor goal
        /// </summary>
        /// <param name="foot_left">position of the left foot</param>
        /// /// <param name="foot_right">position of the right foot</param>
        /// <param name="head">position of the head</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private double DrawFloor(Joint foot_left, Joint foot_right, Joint head, DrawingContext drawingContext)
        {

            Joint center_floor = new Joint();
            center_floor.Position.X = (float)FloorCenterX;
            center_floor.Position.Y = (float)FloorCenterY;
            center_floor.Position.Z = (float)FloorCenterZ;

            // Transform world coordinates to screen coordinates
            DepthSpacePoint depth_center_floor = this.coordinateMapper.MapCameraPointToDepthSpace(center_floor.Position);
            Point center_floor_2D = new Point(depth_center_floor.X, depth_center_floor.Y);

            // If the user is in the good floor position, the ellipse change it color to green
            if ((foot_left.Position.X < FloorCenterX + 0.3 && foot_left.Position.X > FloorCenterX - 0.3) &&
                (foot_right.Position.X < FloorCenterX + 0.3 && foot_right.Position.X > FloorCenterX - 0.3) &&
                (foot_left.Position.Y < FloorCenterY + 0.3 && foot_left.Position.Y > FloorCenterY - 0.3) &&
                (foot_right.Position.Y < FloorCenterY + 0.3 && foot_right.Position.Y > FloorCenterY - 0.3) &&
                (foot_left.Position.Z < FloorCenterZ + 0.3 && foot_left.Position.Z > FloorCenterZ - 0.3) &&
                (foot_right.Position.Z < FloorCenterZ + 0.3 && foot_right.Position.Z > FloorCenterZ - 0.3))
            {
                drawingContext.DrawEllipse(this.goalReachedBrush, null, center_floor_2D, 24, 8);
                advicesText.Text = "¡No te muevas del sitio!";
                // Return the user's height
                return head.Position.Y;
            }
            // If not, the ellipse change it color to red
            else
            {
                if (head.Position.X < FloorCenterX )
                {
                    if (head.Position.Z < FloorCenterZ)
                    {
                        advicesText.Text = "Muévete hacia la derecha y \n hacia atrás";
                    }
                    else if (head.Position.Z > FloorCenterZ)
                    {
                        advicesText.Text = "Muévete hacia la derecha y \n hacia delante";
                    }
                }
                else if (head.Position.X > FloorCenterX)
                {
                    if (head.Position.Z < FloorCenterZ)
                    {
                        advicesText.Text = "Muévete hacia la izquierda y \n hacia atrás";
                    }
                    else if (head.Position.Z > FloorCenterZ)
                    {
                        advicesText.Text = "Muévete hacia la izquierda y \n hacia delante";
                    }
                }
                
                
                drawingContext.DrawEllipse(this.goalNotReachedBrush, null, center_floor_2D, 24, 8);
            }
            // When the user is in a bad floor position, return -10
            return -10;

        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        
    }
}
