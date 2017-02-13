using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using Android.Views;

[assembly: ResolutionGroupName("XamarinDocs")]
[assembly: ExportEffect(typeof(TouchTracking.Droid.TouchEffect), "TouchEffect")]

namespace TouchTracking.Droid
{
    public class TouchEffect : PlatformEffect
    {
        Android.Views.View view;
  //      Action<Element, TouchActionEventArgs> onTouchAction;
        bool capture;
        Func<double, double> fromPixels;

        // Information retained for all elements that attach this effect
        class TouchInfo
        {
            public TouchInfo(Element element, Action<Element, TouchActionEventArgs> onTouchAction)
            {
                Element = element;
                OnTouchAction = onTouchAction;
            }

            public Element Element { private set; get; }
            public Action<Element, TouchActionEventArgs> OnTouchAction { private set; get; }
        };

        // 
        static Dictionary<Android.Views.View, TouchInfo> viewDictionary = new Dictionary<Android.Views.View, TouchInfo>();

         //       static Dictionary<int, Android.Views.View> indexDictionary = new Dictionary<int, Android.Views.View>();


        static Dictionary<int, TouchInfo> idToInfoDictionary = new Dictionary<int, TouchInfo>();
        


        protected override void OnAttached()
        {
            // Get the Android View corresponding to the Element that the effect is attached to
            view = Control == null ? Container : Control;

            // Get access to the TouchEffect class in the PCL
            TouchTracking.TouchEffect touchEffect = 
                (TouchTracking.TouchEffect)Element.Effects.
                    FirstOrDefault(e => e is TouchTracking.TouchEffect);

            if (touchEffect != null && view != null)
            {

                viewDictionary.Add(view, new TouchInfo(Element, touchEffect.OnTouchAction));




                // Save the method to call on touch events
//                onTouchAction = effect.OnTouchAction;                   // Remove

                // Save setting of Capture property
    //            capture = touchEffect.Capture;                               // Move to TouchInfo??? NO, NO, NO. Need to move Capture access to Down handler, if possible

                // Save fromPixels function
                fromPixels = view.Context.FromPixels;

                // Set event handler on View
                view.Touch += OnTouch;
            }
        }

        protected override void OnDetached()
        {
  //          if (onTouchAction != null)
            {
     //           view.Touch -= OnTouch;                              // Remove
            }


            if (viewDictionary.ContainsKey(view))
            {
                viewDictionary.Remove(view);
                view.Touch -= OnTouch;
            }
        }

        void OnTouch(object sender, Android.Views.View.TouchEventArgs args)
        {
            // Two object common to all the events
            Android.Views.View senderView = sender as Android.Views.View;
            MotionEvent motionEvent = args.Event;

            // Get the pointer index
            int pointerIndex = motionEvent.ActionIndex;

            // Get the id that identifies a finger over the course of its progress
            int id = motionEvent.GetPointerId(pointerIndex);

            // Get the TouchInfo for this View
            TouchInfo touchInfo = viewDictionary[senderView];

            // Convert the point to device-independent coordinates
            Point point = new Point(fromPixels(motionEvent.GetX(pointerIndex)),
                                    fromPixels(motionEvent.GetY(pointerIndex)));

            // Use ActionMasked here rather than Action to reduce the number of possibilities
            switch (args.Event.ActionMasked)
            {
                case MotionEventActions.Down:
                case MotionEventActions.PointerDown:
                    // Trigger the Entered and Pressed events
                    touchInfo.OnTouchAction(touchInfo.Element, 
                        new TouchActionEventArgs(id, TouchActionType.Entered, point, true));

                    touchInfo.OnTouchAction(touchInfo.Element, 
                        new TouchActionEventArgs(id, TouchActionType.Pressed, point, true));

                    // Add the TouchInfo to the idToInfoDictonary
                    idToInfoDictionary.Add(id, touchInfo);

                    // Get access to the TouchEffect class in the PCL
                    TouchTracking.TouchEffect touchEffect = 
                        (TouchTracking.TouchEffect)Element.Effects.
                            FirstOrDefault(e => e is TouchTracking.TouchEffect);

                    // Get the Capture property setting from the effect
                    capture = touchEffect.Capture;
                    break;

                case MotionEventActions.Move:
                    // Multiple Move events are bundled, so handle them in a loop
                    for (pointerIndex = 0; pointerIndex < motionEvent.PointerCount; pointerIndex++)
                    {
                        id = motionEvent.GetPointerId(pointerIndex);

                        point = new Point(fromPixels(motionEvent.GetX(pointerIndex)),
                                          fromPixels(motionEvent.GetY(pointerIndex)));


                        if (capture)
                        {
                            //      touchInfo = viewDictionary[senderView];

                            touchInfo.OnTouchAction(touchInfo.Element, 
                                new TouchActionEventArgs(id, TouchActionType.Moved, point, true));
                        }
                        else
                        {
                            // Get the screen location (in pixels) of the Android View generating this event
                            int[] screenLocation = new int[2];
                            senderView.GetLocationOnScreen(screenLocation);

                            // Get the coordinates of the finger relative to the screen
                            Point fingerScreenCoordinates = new Point(fromPixels(screenLocation[0]) + point.X,
                                                                      fromPixels(screenLocation[1]) + point.Y);

                     //       System.Diagnostics.Debug.WriteLine("finger: {0} {1}", fingerScreenCoordinates.X, fingerScreenCoordinates.Y);

                            Android.Views.View viewOver = null;

                            // TODO: Need to replace this...
                            TouchInfo touchInfoOver = null;

                            // ... with this, and then determine which one is one top
                            List<TouchInfo> touchInfosOver = new List<TouchInfo>();

                            // Enumerate Android Views with this effect attached
                            foreach (Android.Views.View view in viewDictionary.Keys)
                            {
                                // Get screen location of this view (use array created earlier)
                                view.GetLocationOnScreen(screenLocation);

                                Rectangle viewRect = new Rectangle(fromPixels(screenLocation[0]),
                                                                   fromPixels(screenLocation[1]),
                                                                   fromPixels(view.Width),
                                                                   fromPixels(view.Height));

                           //     System.Diagnostics.Debug.WriteLine("view: {0} {1} {2} {3}", viewLocation.X, viewLocation.Y, view.Width, view.Height);


                                // Determine if the touch is over this view
                                if (viewRect.Contains(fingerScreenCoordinates))
                                {
                                    viewOver = view;

                                    if (viewDictionary.ContainsKey(viewOver))
                                    {
                                        touchInfoOver = viewDictionary[viewOver];
                                        touchInfosOver.Add(touchInfoOver);
                                    }
                                }
                            }

                            // Switch touch event ids from one element to another.
                            // Either element could be null
                            if (touchInfoOver != idToInfoDictionary[id])
                            {
                                if (idToInfoDictionary[id] != null)
                                {
                                    // TODO: NEED TO CHANGE POINT!!!!!!

                                    




                                    idToInfoDictionary[id].OnTouchAction(idToInfoDictionary[id].Element,
                                        new TouchActionEventArgs(id, TouchActionType.Exited, point, true));
                                }

                                if (touchInfoOver != null)
                                {
                                    // TODO: NEED TO CHANGE POINT!!!!!!

                                    touchInfoOver.OnTouchAction(touchInfoOver.Element,
                                        new TouchActionEventArgs(id, TouchActionType.Entered, point, true));
                                }

                                // Save the new elementOver in the dictionary
                                idToInfoDictionary[id] = touchInfoOver;
                            }

                            if (touchInfoOver != null)
                            {
                                touchInfoOver.OnTouchAction(touchInfoOver.Element,
                                    new TouchActionEventArgs(id, TouchActionType.Moved, point, true));
                            }
                        }
                    }
                    break;


                case MotionEventActions.Up:
                case MotionEventActions.Pointer1Up:
                    if (capture)
                    {
                  //      touchInfo = viewDictionary[senderView];

                        touchInfo.OnTouchAction(touchInfo.Element, 
                            new TouchActionEventArgs(id, TouchActionType.Released, point, true));

                        touchInfo.OnTouchAction(touchInfo.Element, 
                            new TouchActionEventArgs(id, TouchActionType.Exited, point, true));
                    }
                    else
                    {
                        if (idToInfoDictionary[id] != null)
                        {
                            // TODO: Need to convert point!

                            // Can share code with capture OnTouchAction and Element

                       //     TouchInfo touchInfo = idToInfoDictionary[id];
                            touchInfo.OnTouchAction(touchInfo.Element, new TouchActionEventArgs(id, TouchActionType.Released, point, true));
                            touchInfo.OnTouchAction(touchInfo.Element, new TouchActionEventArgs(id, TouchActionType.Exited, point, true));
                        }
                    }
                    idToInfoDictionary.Remove(id);
                    break;

                case MotionEventActions.Cancel:
                    if (capture)
                    {
                        touchInfo.OnTouchAction(touchInfo.Element, 
                            new TouchActionEventArgs(id, TouchActionType.Cancelled, point, true));
                    }
                    else
                    {
                        if (idToInfoDictionary[id] != null)
                        {
                            // TODO: Need to convert point!

                            touchInfo = idToInfoDictionary[id];
                            touchInfo.OnTouchAction(touchInfo.Element, new TouchActionEventArgs(id, TouchActionType.Cancelled, point, true));


                        }
                    }
                    idToInfoDictionary.Remove(id);
                    break;
            }
        }



#if XXX


            for (int pointerIndex = 0; pointerIndex < args.Event.PointerCount; pointerIndex++)
            {
                int id = args.Event.GetPointerId(pointerIndex);
                Point point = new Point(args.Event.GetX(pointerIndex), args.Event.GetY(pointerIndex));
                bool isInContact = true;

                // The Down event is always on the Element stored with the Effect
                if (args.Event.Action == MotionEventActions.Down)
                {
                    onTouchAction(Element, new TouchActionEventArgs(id, TouchActionType.Entered, point, isInContact));
                    onTouchAction(Element, new TouchActionEventArgs(id, TouchActionType.Pressed, point, isInContact));

                    //        indexDictionary.Add(id, senderView);

                    idToInfoDictionary.Add(id, viewDictionary[senderView]);

/*
                    idToInfoDictionary.Add(id, new TouchInfo
                    {
                        Element = Element,
                        OnTouchAction = onTouchAction
                    });
*/
                }
                // Otherwise, find the element that the pointer is over
                else
                {
                    int[] screenLocation = new int[2];
                    senderView.GetLocationOnScreen(screenLocation);

                    Point pointerScreenCoordinates = new Point(screenLocation[0] + point.X, screenLocation[1] + point.Y);
                    Android.Views.View viewOver = null;

                    // Need to replace this ...
                    TouchInfo touchInfoOver = null;

                    // ... with this, and then determine which one is on top
                    List<TouchInfo> touchInfosOver = new List<TouchInfo>();

                    foreach (Android.Views.View view in viewDictionary.Keys)
                    {
                        int[] screenPos = new int[2];
                        view.GetLocationOnScreen(screenPos);




                        if (pointerScreenCoordinates.X >= screenPos[0] && 
                            pointerScreenCoordinates.X < screenPos[0] + view.Width &&
                            pointerScreenCoordinates.Y >= screenPos[1] && 
                            pointerScreenCoordinates.Y < screenPos[1] + view.Height)
                        {
                            viewOver = view;

                            if (viewDictionary.ContainsKey(viewOver))
                            {
                                touchInfoOver = viewDictionary[viewOver];

                                touchInfosOver.Add(touchInfoOver);

                                System.Diagnostics.Debug.WriteLine("{0} {1}", (touchInfoOver.Element as PolySynth.Key).KeyNumber, viewOver.GetZ());
                            }
                            //     break;
                        }
                    }

                    // Switches touch event ids from one element to another. Either Element object could be null
                    if (touchInfoOver != idToInfoDictionary[id])
                    {
                        if (idToInfoDictionary[id] != null)
                        {
                            // TK: NEED TO CHANGE POINT!!!!!

                            System.Diagnostics.Debug.WriteLine("Exit on {0}", (idToInfoDictionary[id].Element as PolySynth.Key).KeyNumber);

                            idToInfoDictionary[id].OnTouchAction(idToInfoDictionary[id].Element, new TouchActionEventArgs(id, TouchActionType.Exited, point, isInContact));
                        }

                        if (touchInfoOver != null)
                        {
                            // TK: NEED TO CHANGE POINT!!!!!

                            System.Diagnostics.Debug.WriteLine("Enter on {0}", (touchInfoOver.Element as PolySynth.Key).KeyNumber);


                            touchInfoOver.OnTouchAction(touchInfoOver.Element, new TouchActionEventArgs(id, TouchActionType.Entered, point, isInContact));
                        }

                        // Save the new elementOver in the dictionary
                        idToInfoDictionary[id] = touchInfoOver;
                    }

                    if (touchInfoOver != null)
                    {
                        

                        switch (args.Event.Action)
                        {
                            case MotionEventActions.Down:
                                //onTouchAction(Element, new TouchActionEventArgs(id, TouchActionType.Entered, point, isInContact));
                                //onTouchAction(Element, new TouchActionEventArgs(id, TouchActionType.Pressed, point, isInContact));
                                //indexDictionary.Add(id, senderView);

                                //idToElementDictionary.Add(id, Element);
                                break;

                            case MotionEventActions.Move:
                                touchInfoOver.OnTouchAction(touchInfoOver.Element, new TouchActionEventArgs(id, TouchActionType.Moved, point, isInContact));
                                break;

                            case MotionEventActions.Up:
                                touchInfoOver.OnTouchAction(touchInfoOver.Element, new TouchActionEventArgs(id, TouchActionType.Released, point, isInContact));
                                touchInfoOver.OnTouchAction(touchInfoOver.Element, new TouchActionEventArgs(id, TouchActionType.Exited, point, isInContact));

                   //             indexDictionary.Remove(id);
                                idToInfoDictionary.Remove(id);
                                break;

                            case MotionEventActions.Cancel:
                                touchInfoOver.OnTouchAction(touchInfoOver.Element, new TouchActionEventArgs(id, TouchActionType.Cancelled, point, isInContact));

                       //         indexDictionary.Remove(id);
                                idToInfoDictionary.Remove(id);
                                break;

                            default:
                                System.Diagnostics.Debug.WriteLine("Touch: " + args.Event.Action);
                                break;
                        }
                    }
                }
#endif
    }

}
