/*                                       Embedded Input Module by Alexander "maylizbeth" Gilbertson / B512325
 *                                                          
 *      EmbeddedInputModule exists to allow better gamepad support for student projects. Designed to be a direct drag and drop replacement for
 *      the classic Input module (LegacyInputModule) using Unity's new InputSystem package to port and give all functions hot-plug device support. 
 *      
 *                                    More information can be found in the GitHub. PROVIDED UNDER THE MIT LICENSE
 *                                                  https://github.com/neopolitans/EmbeddedInputModule
 *      
 *                  This module requires Unity Technologies' INPUT SYSTEM Package. If you do not have it, here's how to install it:
 *                                                      (Remember to save your work and scene first.)
 *      
 *      1. Head to the top bar (where File, Edit, etc... are)
 *      
 *      2. Go to WINDOW > PACKAGE MANAGER (under Asset Store)
 *      
 *      3. Make sure Packages is set to "PACKAGES: UNITY REGISTRY"
 *      
 *      4. Search (or scroll down until you see) "INPUT SYSTEM"
 *      
 *      5. Add that package to your project.
 *      
 *      6. A prompt will appear asking to disable the old Input system, replace it and restart the editor. Click Accept/Yes.
 * 
 *      There's a thing you can do with C# to assign aliases (nicknames) to  classes. In our case this will be the EmbeddedInputModule.
 *      To directly 'replace' Input with the EmbeddedInputModule for any script you want to use it in, add this line near the top of your script:
 *      
 *      using Input = EmbeddedInputModule;
 */

//------------------------------------------------------------------------------------------------------------------------------------------------------------
//                                                              EMBEDDED INPUT MODULE | SETTINGS
//------------------------------------------------------------------------------------------------------------------------------------------------------------

// The following preprocessor directives are settings. Uncomment settings to enable them and comment them to disable them. Each one has a descriptor.
// MSG - Debug Message | OPT - Optimization | CFT - Compatibiltiy Feature

//#define EIM_DISABLEMSG_ActiveGamepadSet                           // DISABLE "Active Gamepad Set as..." MESSAGE

//#define EIM_DISABLEMSG_EnhancedTouchEnabledORDisabled             // DISABLE "Enhanced Touch Already Enabled/Disabled." MESSAGE

//#define EIM_DISABLEMSG_EnhancedTouchNeedsToBeEnabled              // DISABLE "Enhanced Touch Needs to be enabled to use this member or method. Call Input.EnableTouch()" MESSAGE
                                                                    // touchCount, touches, touchSupported and GetTouch require this system to be enabled to be used.

  #define EIM_RESTRICTCFT_anyKeyANDanyKeyDown                       // RESTRICT anyKey and anyKeyDown to only reading Keyboard.current.anyKey.           
                                                                    // Otherwise, after every Input Update each button for each KeyCode is retrieved, using ConvertKeyCode()
                                                                    // then TryGetPressed & TryGetHeld is called for each to determine if any Keyboard, Mouse or Gamepad button is held and
                                                                    // pressed that frame. If *ANY* button is needed, disable this restriction for the full detection at a performance cost.

//#define EIM_RESTRICTCFT_JoystickNameGamepadNamesOnly              // RESTRICT GetJoystickNames() to only get names of found Gamepad devices that are added to the List
                                                                    // m_gamepads (list of DeviceIdentifiers). Micro-optimization if other device names aren't required. 

//#define EIM_DISABLEOPT_SingleGamepadKeycodeCheck                  // DISABLE the single keycode value check in IsAGamepadKeycode(), which is a micro-optimization. 
                                                                    // Use this setting if more keycodes have been added after Keycode 509 that are not Joystick keycodes

  #define EIM_ENABLEOPT_DisableDeviceChangedExtraCases              // DISABLE UsageChagned, ConfiguratonChanged, SoftReset and HardReset cases in DeviceChanged(). 
                                                                    // A micro-optimization if you do not need them as these are left in for extensibility.

//------------------------------------------------------------------------------------------------------------------------------------------------------------
//                                                        EMBEDDED INPUT MODULE | IDE SUPPRESSIONS
//------------------------------------------------------------------------------------------------------------------------------------------------------------

// The following directives are to disable warnings that are going to occur given how this is a drag and drop replacemnet mimicking the LegacyInputModule.
// Don't do this unless there is an extremely good reason to as here these exist to hush the Microsoft IDE.

#pragma warning disable IDE1006         // Disables IDE1006 - Naming rule violation: These words must begin with upper case characters: <...>
                                        // Occurs when any member, method, constructor, class, struct or interface starts with a lower case letter.

//------------------------------------------------------------------------------------------------------------------------------------------------------------

#region EMBEDDED INPUT MANAGER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// To globally mark any EmbeddedInputModule and OptionalModule classes as having accessibility configurations. <br/>
/// This should not be inherited by classes which can be decoupled from EmbeddedInputModule.
/// </summary>
public interface IAccessibilityConfigurable
{
    /// <summary>
    /// Whether the Accessibility Feature(s) of the inheriting class are enabled.
    /// </summary>
    public bool IsAccessibilityEnabled { get; }

    /// <summary>
    /// Enable or Disable the Accessibility Feature(s) of the inheriting class.
    /// </summary>
    /// <param name="setting"></param>
    public void SetAccessibility(bool setting);
}

/// <summary>
/// A class dedicated to handling the new InputSystem and providing methods and members that enable drag-and-drop compatibility with LegacyInputSystem. <br/>
/// Requires Unity's InputSystem package. (1.6.3 or later advised).
/// </summary>
public static class EmbeddedInputModule
{
    #region REGION: ENUMS

    /// <summary>
    /// An Enum describing the type of device.
    /// </summary>
    public enum DeviceType
    {
        None,
        Keyboard,
        Mouse,
        Gamepad,
        Pen,
        Unknown
    }

    /// <summary>
    /// An Enum describing the gamepad type, if the device is a gamepad.<br/>
    /// Otherwise, the value is <see cref="GamepadType.None"/>.
    /// </summary>
    public enum GamepadType
    {
        None,
        Xbox,
        PlayStation,
        Switch,
        Generic
    }

    /// <summary>
    /// The types of input icons to display based on the curret input method.
    /// </summary>
    public enum InputIconDisplayType
    {
        PC,
        Xbox,
        PlayStation,
        Switch,
        Generic
    }

    /// <summary>
    /// A list of available Controller Buttons, minus Start and Select. <br/>
    /// For the best platform compatibility, Start and Select buttons are not available for this.
    /// </summary>
    [System.Serializable]
    public enum GamepadControl
    {
        LeftStickButton,
        RightStickButton,
        LeftShoulder,
        RightShoulder,
        LeftTrigger,
        RightTrigger,
        ButtonNorth,
        ButtonSouth,
        ButtonEast,
        ButtonWest,
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight
    }

    #endregion

    #region REGION: SUB CLASSES
    // DeviceIdentifier has support for Gamepads, Keyboards, Mice and Pens -> it's a wrapper for InputDevice.

    // This is used to better identify Gamepads with margin of error for precision though that is only one use of this class.
    // EmbeddedInputModule does not need this for Keyboard or Mouse support right now.
    // Keyboard.current and Mouse.current are sufficient but may be further expanded upon.
    /// <summary>
    /// An internal class for proper identification of devices.
    /// </summary>
    public class DeviceIdentifier
    {
        /* Internal Variables */
        InputDevice m_device;
        DeviceType m_type = DeviceType.None;
        GamepadType m_gamepadType = GamepadType.None;

        /* Public Variables */
        /// <summary>
        /// Get the Device Type. <br/><br/>
        /// Even when there's no identifier, this will attempt to identify the device <br/>
        /// </summary>
        public DeviceType type {
            get { return m_type; }
        }

        /// <summary>
        /// Get the Gamepad Type, if the type is a gamepad device. <br/><br/>
        /// Even when there's no identifier, this will attempt to identify the device <br/>
        /// </summary>
        public GamepadType gamepadType
        {
            get { return m_gamepadType; }
        }

        /// <summary>
        /// Get the raw InputDevice. <br/><br/>
        /// Even when there's no identifier, this will attempt to identify the device <br/>
        /// </summary>
        public InputDevice device
        {
            get { return m_device; }
        }

        /* Constructors */
        /// <summary>
        /// Create a DeviceIdentifier with only an InputDevice object. <br/>
        /// This will run through IdentifyDevice and attempt to identify the provided device.
        /// </summary>
        /// <param name="inputDevice">The input device to assign to a DeviceIdentifier wrapper object.</param>
        public DeviceIdentifier(InputDevice inputDevice)
        {
            m_device = inputDevice;
            m_type = IdentifyDevice(inputDevice);

            // Try identify the specific platform controller through it's name.
            // Both manufacturers don't give us enough USB HumanInterfaceDevice ("HID") info for us to reliably know the exact platform.
            // Just that they're from one of the overall console franchises (XBOX or PLAYSTATION). 
            if (m_type == DeviceType.Gamepad)
            {
                if (m_device.name.Contains("DualSense"))        // Playstation Controllers usually have "DualSense" in the name. If it isn't directly containing this exact string, it's probably a fake DualSense.
                {
                    m_gamepadType = GamepadType.PlayStation;
                }
                else if (m_device.name.Contains("XInput"))      // XBOX Controllers usually have "XInput" in the name. Microsoft doesn't give us any more information; manufacturer-locked, essentially.
                {
                    m_gamepadType = GamepadType.Xbox;
                }
                else if (m_device.name.Contains("Switch"))
                {
                    m_gamepadType = GamepadType.Switch;
                }
                else                                            // Third-Party Controller. We don't know it's capabilities, just that it exists. Could be compatbile with either the above platforms.
                {
                    m_gamepadType = GamepadType.Generic;
                }
            }
        }

        // This constructer variant is a micro-optimization. As IdentifyDevice is already called whenever
        // a DeviceIdentifier may need to be constructed within this module, the device type is already 
        // known and as such, it would otherwise get called again needlessly.
        /// <summary>
        /// Create a DeviceIdentifier with an InputDevice object and a pre-determined device type. <br/>
        /// This won't call IdentifyDevice as it is assumed the invoker already has the device type through IdentifyDevice or another means.
        /// </summary>
        /// <param name="inputDevice">The input device to assign to a DeviceIdentifier wrapper object.</param>
        /// <param name="deviceType">The input device's type.</param>
        public DeviceIdentifier(InputDevice inputDevice, DeviceType deviceType)
        {
            m_device = inputDevice;
            m_type = deviceType;

            // Try identify the specific platform controller through it's name.
            // Both manufacturers don't give us enough USB HumanInterfaceDevice ("HID") info for us to reliably know the exact platform.
            // Just that they're from one of the overall console franchises (XBOX or PLAYSTATION). 
            if (m_type == DeviceType.Gamepad)
            {
                if (m_device.name.Contains("DualSense"))        // Playstation Controllers usually have "DualSense" in the name. If it isn't directly containing this exact string, it's probably a fake DualSense.
                {
                    m_gamepadType = GamepadType.PlayStation;
                }
                else if (m_device.name.Contains("XInput"))      // XBOX Controllers usually have "XInput" in the name. Microsoft doesn't give us any more information; manufacturer-locked, essentially.
                {
                    m_gamepadType = GamepadType.Xbox;
                }
                else if (m_device.name.Contains("Switch"))
                {
                    m_gamepadType = GamepadType.Switch;
                }
                else                                            // Third-Party Controller. We don't know it's capabilities, just that it exists. Could be compatbile with either the above platforms.
                {
                    m_gamepadType = GamepadType.Generic;
                }
            }
        }

        /* Public methods*/
        /// <summary>
        /// Is this the same device as the comparative device?
        /// </summary>
        /// <param name="comparative">The other device to match against.</param>
        /// <returns></returns>
        public bool SameDevice(InputDevice comparative) => m_device.deviceId == comparative.deviceId;   // IDs are unique and if it is the same ID, we can save time and performance by not completely comparing objects.
        /// <summary>
        /// Is the device a Keyboard?
        /// </summary>
        public bool IsKeyboard => m_type == DeviceType.Keyboard;
        /// <summary>
        /// Is the device a Mouse?
        /// </summary>
        public bool IsMouse => m_type == DeviceType.Mouse;
        /// <summary>
        /// Is the device a Pen Display?
        /// </summary>
        public bool IsPen => m_type == DeviceType.Pen;
        /// <summary>
        /// Is the device a gamepad?
        /// </summary>
        public bool IsGamepad => m_type == DeviceType.Gamepad;
        /// <summary>
        /// Is the device a DualSense controller?
        /// </summary>
        public bool IsDualSense => m_type == DeviceType.Gamepad && m_gamepadType == GamepadType.PlayStation;
        /// <summary>
        /// Is the device an XInput (Xbox) controller?
        /// </summary>
        public bool IsXInput => m_type == DeviceType.Gamepad && m_gamepadType == GamepadType.Xbox;
        /// <summary>
        /// Is the device a Nintendo Switch Compatible controller?
        /// </summary>
        public bool IsSwitch => m_type == DeviceType.Gamepad && m_gamepadType == GamepadType.Switch;

        public string name => m_device.name;

        public int deviceId => m_device.deviceId;

        public bool Null => m_device == null;

        public override string ToString() => $"Device: {m_device.name}\nType: {m_type}\nGamepad Type: {m_gamepadType}";
    }

    // Unused as of yet, planned use for systems that are being investigated with no currently known API translations.
    /// <summary>
    /// An exception thrown when No Corresponding API exists in the Input System yet for translating from LegacyInputModule.
    /// </summary>
    [Serializable]
    public class NoCorrespondingInputSystemAPIException : Exception
    {
        public NoCorrespondingInputSystemAPIException() : base() { }
        public NoCorrespondingInputSystemAPIException(string message) : base(message) { }
        public NoCorrespondingInputSystemAPIException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Interface into the Gyroscope.
    /// </summary>
    /// <remarks>
    /// Replacement static class for the original Input.gyro.
    /// </remarks>
    public static class gyro
    {
        /// <summary>
        /// Try get the current gyroscope component of the device, if one exists.
        /// </summary>
        /// <param name="gyroscope"></param>
        /// <returns></returns>
        public static bool GetGyroscope(out UnityEngine.InputSystem.Gyroscope gyroscope)
        {
            gyroscope = null;
            if (gyroscopePresent)
            {
                gyroscope = UnityEngine.InputSystem.Gyroscope.current;
            }
            return gyroscopePresent;
        }

        /// <summary>
        /// Returns the attitude (ie, orientation in space) of the device. <br/>
        /// If a device does not have an attitude sensor, returns default(Quaternion).
        /// </summary>
        /// <remarks>
        /// To assure a value is returned, check Input.attitudeSensorPresent before calling. <br/>
        /// If EmbeddedInputModule isn't using an alias, replace &lt;Input&gt; with the full name.
        /// </remarks>
        public static Quaternion attitude => attitudeSensorPresent ? AttitudeSensor.current.attitude.TryReadValue(out Quaternion quat) ? quat : default : default;

        /// <summary>
        /// Sets or retrieves the enabled status of this gyroscope. <br/>
        /// Returns the value of UnityEngine.InputSystem.Gyroscope.current.enabled.
        /// </summary>
        public static bool enabled
        {
            get
            {
                return gyroscopePresent ? UnityEngine.InputSystem.Gyroscope.current.enabled : false;
            }
            set
            {
                if (value && gyroscopePresent)
                {
                    InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
                }
                else if (!value && gyroscopePresent) {
                    InputSystem.DisableDevice(UnityEngine.InputSystem.Gyroscope.current);
                }
                else
                {
                    Debug.LogWarning("[EmbeddedInputModule] No Gyroscope on this device.");
                }
            }
        }

        /// <summary>
        /// Returns the gravity acceleration vector expressed in the device's reference frame. <br/>
        /// If a device does not have a gravity sensor, returns default(Vector3).
        /// </summary>
        /// <remarks>
        /// To assure a value is returned, check Input.gravitySensorPresent before calling. <br/>
        /// If EmbeddedInputModule isn't using an alias, replace &lt;Input&gt; with the full name.
        /// </remarks>
        public static Vector3 gravity => gravitySensorPresent ? GravitySensor.current.gravity.TryReadValue(out Vector3 grav) ? grav : default : default;

        /// <summary>
        /// Returns rotation rate as measured by the device's gyroscope.<br/>
        /// If a device does not have a gyroscope, returns default(Vector3).
        /// </summary>
        /// <remarks>
        /// To assure a value is returned, check Input.gyroscopePresent before calling. <br/>
        /// If EmbeddedInputModule isn't using an alias, replace &lt;Input&gt; with the full name.
        /// </remarks>
        public static Vector3 rotationRate => gyroscopePresent ? UnityEngine.InputSystem.Gyroscope.current.angularVelocity.TryReadValue(out Vector3 angVel) ? angVel : default : default;

        // Set: Convert Sampling Frequency back to seconds.     (1 / Hz) gives the interval.
        // Get: Convert the Seconds into a sampling frequency.  (1 / interval) gives the Hz frequency.
        /// <summary>
        /// Sets or retrieves gyroscope interval in seconds.
        /// </summary>
        /// <remarks>
        /// Auto-handles conversion from the samplingFrequency used by InputSystem when setting and getting this value.
        /// </remarks>
        public static float updateInterval
        {
            get
            {   
                return gyroscopePresent ? 1.0f / UnityEngine.InputSystem.Gyroscope.current.samplingFrequency : 0.0f;
            }
            set
            {
                if (gyroscopePresent)
                {
                    UnityEngine.InputSystem.Gyroscope.current.samplingFrequency = 1.0f / value;
                }
            }
        }

        /// <summary>
        /// Returns the acceleration that the user is giving to the device. 
        /// </summary>
        public static Vector3 userAcceleration => linearAccelerationSensorPresent ? LinearAccelerationSensor.current.acceleration.TryReadValue(out Vector3 linearAccel) ? linearAccel : default : default;
    }

    #endregion

    // MAIN CLASS

    // - Public Members
    /// <summary>
    /// Get the Current Gamepad. <br/>
    /// This is the gamepad, Keyboard or Mouse from the list that last had any player input.
    /// </summary>
    public static DeviceIdentifier CurrentGamepad
    {
        get { return m_currentGamepad; }
        private set
        {
            if (value == null) return;

            m_currentGamepad = value;
            if (value.type == DeviceType.Gamepad)
            {
                switch (value.gamepadType)
                {
                    default: PlatformIcons =                        InputIconDisplayType.Generic; break;        // Generic can be any layout the developer is designing for, if it is not a standard layout.
                    case GamepadType.Xbox: PlatformIcons =          InputIconDisplayType.Xbox; break;
                    case GamepadType.PlayStation: PlatformIcons =   InputIconDisplayType.PlayStation; break;
                    case GamepadType.Switch: PlatformIcons =        InputIconDisplayType.Switch; break;
                }
            }
            else
            {
                PlatformIcons = InputIconDisplayType.PC;    // Default value for safety.
            }

            // The following only triggers in the Editor Window, if EIM_DISABLEMSG_ActiveGamepad isn't defined. It lets developers know what to do.
            #if UNITY_EDITOR && !EIM_DISABLEMSG_ActiveGamepadSet 
            Debug.Log($"[EmbeddedInputModule] Active Gamepad Set as {value.name}\nCurrent Platform Icon Layout: {PlatformIcons}");
            #endif
        }
    }

    /// <summary>
    /// What type of icons to display on interface elements. (Read Only)
    /// </summary>
    /// <remarks>
    /// Can also be used to remap control schemes based on the platform.
    /// </remarks>
    public static InputIconDisplayType PlatformIcons
    {
        get { return m_platformIcons; }
        private set { m_platformIcons = value; }
    }

    /// <summary>
    /// Indicates if a gamepad is assigned to <see cref="CurrentGamepad"/> and if the <br/>
    /// underlying device hasn't been nulled by Garbage Collection or disconnect.
    /// </summary>
    public static bool gamepadPresent => m_currentGamepad != null && m_currentGamepad.device != null;   // This cannot be Gamepad.current as all Gamepad inputs and platform identification happens through DeviceIdentifier instead.

    /// <summary>
    /// Indicates if a keyboard device is detected.
    /// </summary>
    public static bool keyboardPresent => Keyboard.current != null;

    /// <summary>
    /// Indicates if a mouse device is detected.
    /// </summary>
    public static bool mousePresent => Mouse.current != null;

    /// <summary>
    /// Indicates if an accelerometer component is detected.
    /// </summary>
    public static bool accelerometerPresent => Accelerometer.current != null;

    /// <summary>
    /// Indicates if a gyroscope component is detected.
    /// </summary>
    public static bool gyroscopePresent => UnityEngine.InputSystem.Gyroscope.current != null;

    /// <summary>
    /// Indicates if an attitude sensor is detected.<br/>
    /// Used for obtaining the device's orientation in space.
    /// </summary>
    public static bool attitudeSensorPresent => AttitudeSensor.current != null;

    /// <summary>
    /// Indicates if a gravity sensor is detected.<br/>
    /// Used for obtaining the device's orientation in space.
    /// </summary>
    public static bool gravitySensorPresent => GravitySensor.current != null;

    /// <summary>
    /// Indicates if a linear acceleration sensor is detected.
    /// </summary>
    /// <remarks>
    /// An accelerometer that's not affected by gravity.
    /// </remarks>
    public static bool linearAccelerationSensorPresent => LinearAccelerationSensor.current != null;

    /// <summary>
    /// This property controls if input sensors should be compensated for screen orientation.
    /// </summary>
    /// <remarks>
    /// <b>Redirects to InputSystem.settings.compensateForScreenOrientation: </b><br/>
    /// If true, sensors that deliver rotation values on handheld devices will automatically adjust rotations when the screen orientation changes.
    /// </remarks>
    public static bool compensateSensors
    {
        get
        {
            return InputSystem.settings.compensateForScreenOrientation;
        }
        set
        {
            InputSystem.settings.compensateForScreenOrientation = value;
        }
    }

    /// <summary>
    /// Set the current text input position used by IMEs to open windows.
    /// </summary>
    /// <remarks>
    /// Cursor Position isn't stored by InputSystem and so EmbeddedInputModule can't access it. <br/>
    /// Tracking the value for developers may be inconsistent or incorrect. If so, please submit an Issue on GitHub with an example project for testing.
    /// </remarks>
    public static Vector2 compositionCursorPos
    {
        get { return m_lastIMECursorPosition; }
        set
        {
            if (keyboardPresent) SetIMECursorPosition(value);
        }
    }

    /// <summary>
    /// Does the user have an IME input source selected?
    /// </summary>
    /// <remarks>
    /// Utilises InputSystem's Keyboard.current.imeSelected and Keyboard.current.SetIMEEnabled().<br/>
    /// If this behaviour is incorrect, please submit an Issue on the GitHub.
    /// </remarks>
    public static bool imeIsSelected
    {
        get
        {
            return keyboardPresent ? (Keyboard.current.imeSelected.TryReadValue(out float value) && value == 1.0f ? true : false) : false;
        }
        set
        {
            if (keyboardPresent)
            {
                Keyboard.current.SetIMEEnabled(value);
            }
        }
    }

    /// <summary>
    /// The current IME composition string being typed by the user.
    /// </summary>
    public static string compositionString
    {
        get
        {
            return m_imeCompositionString;
        }
    }

    /// <summary>
    /// Returns the keyboard entered this frame. (Read Only)
    /// </summary>
    /// <remarks>
    /// This property returns the input provided via the keyboard converted from <see cref="char"/> to <see cref="string"/>. <br/>
    /// An event hooked to Keyboard.current sets a private member of EmbeddedInputModule
    /// </remarks>
    public static string inputString => m_frameInputString;

    /// <summary>
    /// Returns a list of objects representing the statuses of all touches during last frame. (Read Only)
    /// </summary>
    /// <remarks>
    /// This converts the ReadOnlyArray&lt;Touch&gt; into the traditional array class type. <br/>
    /// The ReadOnlyArray can be used directly instead, however this is provided for familiarity's sake.
    /// </remarks>
    public static UnityEngine.InputSystem.EnhancedTouch.Touch[] touches
    {
        get
        {
            if (EnhancedTouchSupport.enabled) { return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.ToArray(); }
            else
            {
                #if UNITY_EDITOR && !EIM_DISABLEMSG_EnhancedTouchNeedsToBeEnabled
                Debug.Log("[EmbeddedInputModule] Enhanced Touch Support needs to be enabled to use this member or method.\nUse Input.EnableTouch() to enable InputSystem touch support.");
                #endif
                return default;
            }
        }
    }

    /// <summary>
    /// Number of touches. (Read Only)
    /// </summary>
    public static int touchCount
    {
        get
        {
            if (EnhancedTouchSupport.enabled) { return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count; }
            else
            {
                #if UNITY_EDITOR && !EIM_DISABLEMSG_EnhancedTouchNeedsToBeEnabled
                Debug.Log("[EmbeddedInputModule] Enhanced Touch Support needs to be enabled to use this member or method.\nUse Input.EnableTouch() to enable InputSystem touch support.");
                #endif
                return 0;
            }
        }
    }

    /// <summary>
    /// Returns whether the device running the application supports touch input.
    /// </summary>
    public static bool touchSupported => Touchscreen.current != null;

#if !EIM_RESTRICTCFT_anyKeyANDanyKeyDown
    /// <summary>
    /// Is any key or mouse button currently held down? (Read Only)
    /// </summary>
    public static bool anyKey => m_anyKeyHeld;

    /// <summary>
    /// Returns true the first frame the user hits any key or mouse button. (Read Only)
    /// </summary>
    public static bool anyKeyDown => m_anyKeyPressedThisFrame;
#else
    // anyKey.wasUpdatedThisFrame does exist, however isPressed and wasPressedThisFrame are the two values actively sought.

    /// <summary>
    /// Is any key or mouse button currently held down? (Read Only)
    /// </summary>
    /// <remarks>
    /// This only returns the value of <see cref="Keyboard.current.anyKey"/> if there is a keyboard connected. Otherwise, false. <br/><br/>
    /// To get full detection for any button press at a performance cost, disable the preprocessor directive <br/>
    /// &lt;<b>EIM_RESTRICTCFT_anyKeyANDanyKeyDown</b>&gt; in EmbeddedInputModule.cs.
    /// </remarks>
    public static bool anyKey => keyboardPresent ? (Keyboard.current.anyKey.TryGetHeld(out bool held) ? held : false) : false;

    /// <summary>
    /// Returns true the first frame the user hits any key or mouse button. (Read Only)
    /// </summary>
    /// <remarks>
    /// This only returns the value of <see cref="Keyboard.current.anyKey"/> if there is a keyboard connected. Otherwise, false. <br/><br/>
    /// To get full detection for any button press at a performance cost, disable the preprocessor directive <br/>
    /// &lt;<b>EIM_RESTRICTCFT_anyKeyANDanyKeyDown</b>&gt; in EmbeddedInputModule.cs.
    /// </remarks>
    public static bool anyKeyDown => keyboardPresent ? (Keyboard.current.anyKey.TryGetPressed(out bool held) ? held : false) : false;
    #endif

// - Private Members
    #if !EIM_RESTRICTCFT_anyKeyANDanyKeyDown    
    /// <summary>
    /// The private value set internally for if there is any button currently pressed down.
    /// </summary>
    static bool m_anyKeyHeld;

    /// <summary>
    /// The private value set internally for if there is any button pressed down this frame.
    /// </summary>
    static bool m_anyKeyPressedThisFrame;
    #endif

    /// <summary>
    /// The list of gamepads connected.
    /// </summary>
    static List<DeviceIdentifier> m_gamepads = new List<DeviceIdentifier>();

    /// <summary>
    /// The Current Input Device.
    /// </summary>
    static DeviceIdentifier m_currentGamepad; 

    /// <summary>
    /// The icon display type to set.
    /// </summary>
    static InputIconDisplayType m_platformIcons = InputIconDisplayType.PC;

    static Vector2 m_lastIMECursorPosition = Vector2.zero;

    static string m_imeCompositionString;

    static string m_frameInputString;

    // - Public Methods

    /// <summary>
    /// Returns true while the user holds down the key identified by the <b><i>key</i></b> <see cref="KeyCode"/> enum parameter. <br/><br/>
    /// <b>Joystick Support:</b><br/>
    /// Any joystick buttons map to the current gamepad. They are mapped relative to the order they appear in the Controller Inputs section of the EmbeddedInputModule.<br/>
    /// Joystick buttons 16 through 19 do not have a corresponding map. All other controls are documented otherwise.<br/><br/>
    /// 
    /// <b>Support Limitations of KeyCode:</b><br/>
    /// - Does not have support for OEM Keys (oem1Key to oem5Key) as there are no equivalent KeyCodes. <br/>
    /// - Does not support ctrlKey or leftKey. These represent if either the Left or Right Control/Shift keys are pressed. <br/>
    /// - Mouse5 and Mouse6 are not supported by InputSystem. <br/>
    /// - Some keys (such as Tilde) are not supported by InputSystem. See InputSystem API for more information. 
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns></returns>
    public static bool GetKey(KeyCode key)
    {
        if (ConvertKeyCode(key, out ButtonControl control))
        {
            if (TryGetHeld(control, out bool value)) return value;
            else return false;
        }
        else return false;
    }

    /// <summary>
    /// Returns true during the frame the user starts pressing down the key identified by the <b><i>key</i></b> <see cref="KeyCode"/> enum parameter. <br/><br/>
    /// <b>Joystick Support:</b><br/>
    /// Any joystick buttons map to the current gamepad. They are mapped relative to the order they appear in the Controller Inputs section of the EmbeddedInputModule.<br/>
    /// Joystick buttons 16 through 19 do not have a corresponding map. All other controls are documented otherwise.<br/><br/>
    /// 
    /// <b>Support Limitations of KeyCode:</b><br/>
    /// - Does not have support for OEM Keys (oem1Key to oem5Key) as there are no equivalent KeyCodes. <br/>
    /// - Does not support ctrlKey or leftKey. These represent if either the Left or Right Control/Shift keys are pressed. <br/>
    /// - Mouse5 and Mouse6 are not supported by InputSystem. <br/>
    /// - Some keys (such as Tilde) are not supported by InputSystem. See InputSystem API for more information. 
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns></returns>
    public static bool GetKeyDown(KeyCode key)
    {
        if (ConvertKeyCode(key, out ButtonControl control))
        {
            if (TryGetPressed(control, out bool value)) return value;
            else return false;
        }
        else return false;
    }

    /// <summary>
    /// Returns true during the frame the user releases the key identified by the <b><i>key</i></b> <see cref="KeyCode"/> enum parameter. <br/><br/>
    /// <b>Joystick Support:</b><br/>
    /// Any joystick buttons map to the current gamepad. They are mapped relative to the order they appear in the Controller Inputs section of the EmbeddedInputModule.<br/>
    /// Joystick buttons 16 through 19 do not have a corresponding map. All other controls are documented otherwise.<br/><br/>
    /// 
    /// <b>Support Limitations of KeyCode:</b><br/>
    /// - Does not have support for OEM Keys (oem1Key to oem5Key) as there are no equivalent KeyCodes. <br/>
    /// - Does not support ctrlKey or leftKey. These represent if either the Left or Right Control/Shift keys are pressed. <br/>
    /// - Mouse5 and Mouse6 are not supported by InputSystem. <br/>
    /// - Some keys (such as Tilde) are not supported by InputSystem. See InputSystem API for more information. 
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns></returns>
    public static bool GetKeyUp(KeyCode key)
    {
        if (ConvertKeyCode(key, out ButtonControl control))
        {
            if (TryGetPressed(control, out bool value)) return value;
            else return false;
        }
        else return false;
    }

    /// <summary>
    /// Returns whether the given mouse button is held down.
    /// </summary>
    /// <remarks>
    /// <b>Supports:</b> Left Mouse [0], Right Mouse [1], Middle Mouse [2], Mouse 4 [3], Mouse 5 [4]. <br/>
    /// </remarks>
    /// <param name="button">The mouse button to check.</param>
    /// <returns></returns>
    public static bool GetMouseButton(int button)
    {
        if (!mousePresent) return false;

        // Condensing these methosd into ternary operations helps a lot.
        switch (button)
        {
            default: return false;
            case 0: return Mouse.current.leftButton.TryGetHeld(out bool leftMouseHeld) ? leftMouseHeld : false;
            case 1: return Mouse.current.rightButton.TryGetHeld(out bool rightMouseHeld) ? rightMouseHeld : false;
            case 2: return Mouse.current.middleButton.TryGetHeld(out bool middleMouseHeld) ? middleMouseHeld : false;
            case 3: return Mouse.current.backButton.TryGetHeld(out bool mouse4Held) ? mouse4Held : false;
            case 4: return Mouse.current.forwardButton.TryGetHeld(out bool mouse5Held) ? mouse5Held : false;
        }
    }

    /// <summary>
    /// Returns true during the frame the user pressed the given mouse button.
    /// </summary>
    /// <remarks>
    /// <b>Supports:</b> Left Mouse [0], Right Mouse [1], Middle Mouse [2], Mouse 4 [3], Mouse 5 [4]. <br/>
    /// </remarks>
    /// <param name="button">The mouse button to check.</param>
    /// <returns></returns>
    public static bool GetMouseButtonDown(int button)
    {
        if (!mousePresent) return false;

        switch (button)
        {
            default: return false;
            case 0: return Mouse.current.leftButton.TryGetPressed(out bool leftMouseHeld) ? leftMouseHeld : false;
            case 1: return Mouse.current.rightButton.TryGetPressed(out bool rightMouseHeld) ? rightMouseHeld : false;
            case 2: return Mouse.current.middleButton.TryGetPressed(out bool middleMouseHeld) ? middleMouseHeld : false;
            case 3: return Mouse.current.backButton.TryGetPressed(out bool mouse4Held) ? mouse4Held : false;
            case 4: return Mouse.current.forwardButton.TryGetPressed(out bool mouse5Held) ? mouse5Held : false;
        }
    }

    /// <summary>
    /// Returns true during the frame the user released the given mouse button.
    /// </summary>
    /// <remarks>
    /// <b>Supports:</b> Left Mouse [0], Right Mouse [1], Middle Mouse [2], Mouse 4 [3], Mouse 5 [4]. <br/>
    /// </remarks>
    /// <param name="button">The mouse button to check.</param>
    /// <returns></returns>
    public static bool GetMouseButtonUp(int button)
    {
        if (!mousePresent) return false;

        switch (button)
        {
            default: return false;
            case 0: return Mouse.current.leftButton.TryGetReleased(out bool leftMouseHeld) ? leftMouseHeld : false;
            case 1: return Mouse.current.rightButton.TryGetReleased(out bool rightMouseHeld) ? rightMouseHeld : false;
            case 2: return Mouse.current.middleButton.TryGetReleased(out bool middleMouseHeld) ? middleMouseHeld : false;
            case 3: return Mouse.current.backButton.TryGetReleased(out bool mouse4Held) ? mouse4Held : false;
            case 4: return Mouse.current.forwardButton.TryGetReleased(out bool mouse5Held) ? mouse5Held : false;
        }
    }

    /// <summary>
    /// Returns an array of joystick and gamepad device names. <br/>
    /// This will return a list of the names of every joystick or gamepad detected by EmbeddedInputManager.
    /// </summary>
    /// <remarks>
    /// <i>Included for drag-and-drop compatibility with LegacyInputModule.</i>
    /// </remarks>
    /// <returns></returns>
    public static string[] GetJoystickNames()
    {
#if EIM_RESTRICTCFT_JoystickNameGamepadNamesOnly
        string[] result = new string[m_gamepads.Count];

        for (int i = 0; i < m_gamepads.Count; i++)
        {
            result[i] = m_gamepads[i].name;
        }

        return result;
#else
        string[] result = new string[InputSystem.devices.Count];

        for (int i = 0; i < InputSystem.devices.Count; i++)
        {
            result[i] = InputSystem.devices[i].name;
        }

        return result;
#endif
    }

    /// <summary>
    /// Get the last touch input in the current array of touches reported by the device screen.
    /// </summary>
    /// <returns></returns>
    public static UnityEngine.InputSystem.EnhancedTouch.Touch GetTouch()
    {
        if (EnhancedTouchSupport.enabled) { return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count - 1]; }
        else
        {
            #if UNITY_EDITOR && !EIM_DISABLEMSG_EnhancedTouchNeedsToBeEnabled
            Debug.Log("[EmbeddedInputModule] Enhanced Touch Support needs to be enabled to use this member or method.\nUse Input.EnableTouch() to enable InputSystem touch support.");
            #endif
            return default;
        }
    }

    /// <summary>
    /// Get the touch input at the given index in the current array of touches reported by the device screen.
    /// </summary>
    /// <returns></returns>
    public static UnityEngine.InputSystem.EnhancedTouch.Touch GetTouch(int index)
    {
        if (EnhancedTouchSupport.enabled) 
        {
            if (index >= 0 && index < UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count) return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[index];
            else throw new IndexOutOfRangeException("[EmbeddedInputModule] Touch Index provided is outside the range of the amount of touch values present in the active touches array.");
        }
        else
        {
            #if UNITY_EDITOR && !EIM_DISABLEMSG_EnhancedTouchNeedsToBeEnabled
            Debug.Log("[EmbeddedInputModule] Enhanced Touch Support needs to be enabled to use this member or method.\nUse Input.EnableTouch() to enable InputSystem touch support.");
            #endif
            return default;
        }
    }

    // For EnableTouch and DisableTouch, if the direct methods for EnhancedTouchSupport are called multiple times on one (i.e called Enabled 5 times)
    // it will take 5 calls to DisableTouch to disable touch support. These two methods have been given simple safeguards to prevent this.
    /// <summary>
    /// A redirect for enabling touch instead of directly typing InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable(); <br/>
    /// Also checks if support is already enabled as a safeguard measure.
    /// </summary>
    /// <remarks>
    /// The following setting exists to toggle the display of a message for if EnhancedTouch is already enabled. <br/>
    /// EmbeddedInputModule.cs -&gt; Preprocessor Directives -&gt; <b>EIM_DISABLEMSG_EnhancedTouchEnabledORDisabled</b>
    /// </remarks>
    public static void EnableTouch()
    {
        if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        #if UNITY_EDITOR && !EIM_DISABLEMSG_EnhancedTouchEnabledORDisabled
        Debug.Log("[EmbeddedInputModule] Enhanced Touch Support already enabled.");
        #endif
    }

    /// <summary>
    /// A redirect for disabling touch instead of directly typing InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
    /// </summary>
    /// <remarks>
    /// The following setting exists to toggle the display of a message for if EnhancedTouch is already disabled. <br/>
    /// EmbeddedInputModule.cs -&gt; Preprocessor Directives -&gt; <b>EIM_DISABLEMSG_EnhancedTouchEnabledORDisabled</b>
    /// </remarks>
    public static void DisableTouch()
    {
        if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
        #if UNITY_EDITOR && !EIM_DISABLEMSG_EnhancedTouchEnabledORDisabled
        Debug.Log("[EmbeddedInputModule] Enhanced Touch Support already disabled.");
        #endif
    }

    // - Private Methods
    // - - State Management

    /// <summary>
    /// <see langword="[INTERNAL]"/> Initialise and link up the Embedded Input Manager <br/>
    /// This will be triggered automatically and go through all input devices to find gamepads.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    //[ExecuteAlways]
    static void InitialiseManager()
    {
        InputDevice[] inputDevices = InputSystem.devices.ToArray();

        foreach (InputDevice device in inputDevices)
        {
            DeviceType currentDeviceType = IdentifyDevice(device);
            m_gamepads.Add(new DeviceIdentifier(device, currentDeviceType));
        }

        InputSystem.onDeviceChange += DeviceChanged;

        // Bind a method which, for any button press, determines if that button press comes from a different device.
        // This enables the hot-swapping feature in EmbeddedInputModule.
        InputSystem.onAnyButtonPress.Call(DetermineDeviceChangeOnAnyInput);
        //InputSystem.onAnyButtonPress.Call();
        
        if (keyboardPresent)
        {
            Keyboard.current.onIMECompositionChange += OnIMECompositionChange;
            Keyboard.current.onTextInput += OnKeyboardTextInput;
        }

#if !EIM_RESTRICTCFT_anyKeyANDanyKeyDown
        InputSystem.onAfterUpdate += AfterInputSystemUpdate;
#endif
    }

#if !EIM_RESTRICTCFT_anyKeyANDanyKeyDown
    /// <summary>
    /// Evaluates every known key to determine if any control is pressed. <br/>
    /// Not particularly performant, however no other way around doing this given the inconsistency of InputSystem.onEvent.
    /// </summary>
    static void AfterInputSystemUpdate()
    {
        m_anyKeyHeld = false;
        m_anyKeyPressedThisFrame = false;

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (m_anyKeyHeld && m_anyKeyPressedThisFrame) { break; }
            if (ConvertKeyCode(key, out ButtonControl control))
            {
                if (!m_anyKeyPressedThisFrame && control.TryGetPressed(out bool pressed)) m_anyKeyPressedThisFrame = pressed;
                if (!m_anyKeyHeld && control.TryGetHeld(out bool held)) m_anyKeyHeld = held;
            }
        }
    }
#endif

    // - - Input Management

    // Moved from DeviceIdentifier for better use.
    // Allows the identification of any device plugged in. There is a margin of error but this is a method for some form of reliable identification.
    // DualShockGamepad, SwitchProControllerHID and XInputController classes exist, however they may be restricted to OEM-Only controllers. Any third-party controllers can be identified here.
    /// <summary>
    /// Identify the subtype of a device.
    /// </summary>
    /// <param name="device"></param>
    /// <returns></returns>
    static DeviceType IdentifyDevice(InputDevice device)
    {
        // If there's no desrcriptor for the Device Class, we have to do some searching to figure out if has that capability.
        // Otherwise, we can make assumptions based on the device's class, which is put into a switch statement.
        if (string.IsNullOrEmpty(device.description.deviceClass.ToString()))
        {
            // If one of these returns true, we can assume it's a gamepad of sorts.
            // Otherwise, it's unknown - possibly a custom input.
            if (device.TryGetChildControl("leftStick") != null || device.TryGetChildControl("rightStick") != null)
            {
                return DeviceType.Gamepad;
            }
            else
            {
                return DeviceType.Unknown;
            }
        }
        else
        {
            switch (device.description.deviceClass.ToString())
            {
                default:                return DeviceType.None;
                case "Gamepad":         return DeviceType.Gamepad;
                case "Keyboard":        return DeviceType.Keyboard;
                case "Mouse":           return DeviceType.Mouse;
                case "Pen":             return DeviceType.Pen;
            }
        }
    }

    /// <summary>
    /// Try to find the InputDevice as a DeviceIdentifier in the list of gamepads by comparing device IDs.
    /// </summary>
    /// <param name="device">The device to compare IDs of.</param>
    /// <param name="identity">The DeviceIdentifier, if one is found.</param>
    /// <returns><see langword="bool"/></returns>
    static bool TryFindDeviceIdentifierFromInputDevice(InputDevice device, out DeviceIdentifier identity)
    {
        identity = null;
        foreach (DeviceIdentifier di in m_gamepads)
        {
            if (di.SameDevice(device))
            {
                identity = di; return true;
            }
        }

        return false;
    }

    /// <summary>
    /// If any input occurs, this checks if the gamepad and PlatformIcons should change.
    /// </summary>
    /// <param name="control">The Input object reference.</param>
    static void DetermineDeviceChangeOnAnyInput(InputControl control)
    {
        // If there was no input device change change, don't bother trying to do anything.
        // This is the known efficient method of checking.
        // No deviceIds are the same and are assigned once throughout application lifetime. 
        if (CurrentGamepad != null && control.device.deviceId == CurrentGamepad.device.deviceId) return;
        else
        {
            DeviceType type = TryFindDeviceIdentifierFromInputDevice(control.device, out DeviceIdentifier identifier) ? identifier.type : IdentifyDevice(control.device);
            // Try set the input device
            switch (type)
            {
                // Update 
                default:

                    // TryFindDeviceIdentifierFromInputDevice returned an identifier, extra instructions have been avoided and just pass-by-reference directly.
                    // Can't do anything if a Device Identifier wasn't found. Comparing names won't work if multiple of the same controller brand are connected.
                    if (identifier != null)
                    {
                        CurrentGamepad = identifier;
                    }
                    break;
            }
        }
    }

    // Side Note: Something Unity does in-editor which triggers device changes is it intercepts input from the keyboard or mouse. (Common Behaviour that is known and expected)
    //            This unbinds them from the game viewport and there is a chance to have an error with OnIMECompositionString being unbound or rebound to Keyboard.Current as a result.
    /// <summary>
    /// Used to determine the actions to perform when a device change happens.
    /// </summary>
    /// <param name="device">The device affected by the change.</param>
    /// <param name="change">The type of change that occured.</param>
    static void DeviceChanged(InputDevice device, InputDeviceChange change)
    {
        // This uses a uniquely helpful element of C# - Case fallthrough in switch statements.
        // Because behaviour is similar for the grouped cases (Enabled, Reconnected, Added) and (Disabled, Disconnected, Removed)
        // We can safely fall through the cases and reuse the same code without incurring much, if any, performance penalty.

        // UsageChanged, ConfigurationChanged, SoftReset and HardReset currently have no use.

        switch (change)
        {
            default: break;
            case InputDeviceChange.Enabled:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Added:
                if (device == Keyboard.current)
                {
                    Keyboard.current.onIMECompositionChange += OnIMECompositionChange;
                    Keyboard.current.onTextInput += OnKeyboardTextInput;
                }
                else
                {
                    DeviceType currentDeviceType = IdentifyDevice(device);
                    if (currentDeviceType == DeviceType.Gamepad) { m_gamepads.Add(new DeviceIdentifier(device, currentDeviceType)); }
                }
                break;

            case InputDeviceChange.Disabled:
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Removed:
                if (device == Keyboard.current)
                {
                    Keyboard.current.onIMECompositionChange -= OnIMECompositionChange;
                    Keyboard.current.onTextInput -= OnKeyboardTextInput;
                }
                else
                {
                    // Filter through device IDs to see if the removed one is in the list.
                    // (?) Could replace with m_gamepads.Contains? The objects are different but internals are identical.
                    DeviceIdentifier deviceID = null;
                    foreach (DeviceIdentifier id in m_gamepads)
                    {
                        if (!id.SameDevice(device)) continue;
                        deviceID = id; break;
                    }

                    if (deviceID != null) m_gamepads.Remove(deviceID);
                    if (m_currentGamepad == deviceID) CurrentGamepad = null;
                }
                break;

#if !EIM_ENABLEOPT_DisableDeviceChangedExtraCases
            case InputDeviceChange.UsageChanged:
                break;
            case InputDeviceChange.ConfigurationChanged:
                break;
            case InputDeviceChange.SoftReset:
                break;
            case InputDeviceChange.HardReset:
                break;
#endif
        }
    }

    // This method was created so getting values and catching errors is more consistent.
    // TValue is used InputControl<TValue> but originates from C# Dictionary<TKey, TValue>
    // and represents any Value of Generic Type "T" within a Dictionary key-value pair.

    // Only works for the InputControl<TValue> type. Know the values you're expecting to get from a device [Gamepad, Mouse, Pen, etc] before using this!
    // Read More here: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/api/UnityEngine.InputSystem.InputControl-1.html
    /// <summary>
    /// Try read the encapsulated Vector2 value. Returns true if the value was read successfully.
    /// </summary>
    /// <param name="control">The InputControl to get the value of.</param>
    /// <param name="value">The output value to write to.</param>
    /// <remarks>
    /// Can be supplied as a method parameter or do <b>inputControlObject.TryReadValue(out ExpectedType valueName)</b>. /
    /// </remarks>
    /// <returns><see langword="bool"/></returns>
    static bool TryReadValue<TValue>(this InputControl<TValue> control, out TValue value) where TValue : struct
    {
        value = default;                        // Set the value as the default for the type.
        bool gotValue;                          // Hold whether a value was obtained in this boolean.

        try { 
            value = control.ReadValue();        // Try to read the output.
            gotValue = true;                    // If successful, set gotValue to true.
        } 
        catch (System.Exception) { 
            gotValue = false;                   // Otherwise, an exception was thrown and set gotValue to false.
        };

        return gotValue;
    }

    // The following two methods needed to be derived from TryReadValue because of a quirk of ButtonControl.
    // ButtonControl IS a type of InputControl<TValue>, being InputControl<Single>. However, InputControl<Single>
    // and ButtonControl can only use TryReadValue for the float value, not for isPressed or wasPressedThisFrame.
    // The comparison could still be done manually, however Unity already handles this. A try-catch is unavoidable
    // and just reading what Unity has handled instead of reading + comparing values manually is more performant.

    // These also work for KeyControl objects as they inherit from ButtonControl.

    /// <summary>
    /// Try read if the button is currently held down. Returns true if the value was read successfully.
    /// </summary>
    /// <param name="control">The ButtonControl to read from.</param>
    /// <param name="value">The output value to write to.</param>
    /// <remarks>
    /// Can be supplied as a method parameter or do <b>buttonControlObject.TryReadValue(out ExpectedType valueName)</b>. /
    /// </remarks>
    /// <returns><see langword="bool"/></returns>
    static bool TryGetHeld(this ButtonControl control, out bool value)
    {
        value = default;                        // Set the value as the default for the type.
        bool gotValue;                          // Hold whether a value was obtained in this boolean.

        try
        {
            value = control.isPressed;          // Try to read the isPressed value.
            gotValue = true;                    // If successful, set gotValue to true.
        }
        catch (System.Exception)
        {
            gotValue = false;                   // Otherwise, an exception was thrown and set gotValue to false.
        };

        return gotValue;
    }

    /// <summary>
    /// Try read if the button is pressed this frame. Returns true if the value was read successfully.
    /// </summary>
    /// <param name="control">The ButtonControl to read from.</param>
    /// <param name="value">The output value to write to.</param>
    /// <remarks>
    /// Can be supplied as a method parameter or do <b>buttonControlObject.TryReadValue(out ExpectedType valueName)</b>. /
    /// </remarks>
    /// <returns><see langword="bool"/></returns>
    static bool TryGetPressed(this ButtonControl control, out bool value)
    {
        value = default;                            // Set the value as the default for the type.
        bool gotValue;                              // Hold whether a value was obtained in this boolean.

        try
        {
            value = control.wasPressedThisFrame;    // Try to read the wasPressedThisFrame value.
            gotValue = true;                        // If successful, set gotValue to true.
        }
        catch (System.Exception)
        {
            gotValue = false;                       // Otherwise, an exception was thrown and set gotValue to false.
        };

        return gotValue;
    }

    /// <summary>
    /// Try read if the button is released this frame. Returns true if the value was read successfully.
    /// </summary>
    /// <param name="control">The ButtonControl to read from.</param>
    /// <param name="value">The output value to write to.</param>
    /// <remarks>
    /// Can be supplied as a method parameter or do <b>buttonControlObject.TryReadValue(out ExpectedType valueName)</b>. /
    /// </remarks>
    /// <returns><see langword="bool"/></returns>
    static bool TryGetReleased(this ButtonControl control, out bool value)
    {
        value = default;                            // Set the value as the default for the type.
        bool gotValue;                              // Hold whether a value was obtained in this boolean.

        try
        {
            value = control.wasReleasedThisFrame;    // Try to read the wasReleasedThisFrame value.
            gotValue = true;                        // If successful, set gotValue to true.
        }
        catch (System.Exception)
        {
            gotValue = false;                       // Otherwise, an exception was thrown and set gotValue to false.
        };

        return gotValue;
    }

    #region KeyCode Related Methods
    // Keyboard Keycodes are assigned from 0 to 322.
    // 0 (KeyCode.None) is counted as a keyboard key otherwise and will return false if there is no Keyboard device connected.
    /// <summary>
    /// Is the provided KeyCode a gamepad KeyCode?
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    static bool IsAKeyboardKeycode(KeyCode key) => (int)key < 323;

    // Mouse Keycodes are assigned between values 323 and 329 in the Keycode Enum. Making this a valid somewhat quick and performant check.
    /// <summary>
    /// Is the provided KeyCode a mouse KeyCode?
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    static bool IsAMouseKeycode(KeyCode key) => (int)key >= 323 && (int)key <= 329;

    // Gamepad Keycodes are assigned from Value 330 and onwards. The last keycode assigned is 509 as of Unity 2022.3.2f1, which is Joystick8Button19.
    /// <summary>
    /// Is the provided KeyCode a gamepad KeyCode?
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
#if !EIM_DISABLEOPT_SingleGamepadKeycodeCheck
    static bool IsAGamepadKeycode(KeyCode key) => (int)key >= 330;
#else
    static bool IsAGamepadKeycode(KeyCode key) => (int)key >= 330 && (int)key <= 509;
#endif

    /// <summary>
    /// Return the corresponding InputControl&lt;<see langword="float"/>&gt; the key identified by the <b><i>key</i></b> <see cref="KeyCode"/> enum parameter. <br/><br/>
    /// <b>Joystick Support:</b><br/>
    /// Any joystick buttons map to the current gamepad. They are mapped relative to the order they appear in the Controller Inputs section of the EmbeddedInputModule.<br/>
    /// Joystick buttons 16 through 19 do not have a corresponding map. All other controls are documented otherwise.<br/><br/>
    /// 
    /// <b>Support Limitations of KeyCode:</b><br/>
    /// - Does not have support for OEM Keys (oem1Key to oem5Key) as there are no equivalent KeyCodes. <br/>
    /// - Does not support ctrlKey or leftKey. These represent if either the Left or Right Control/Shift keys are pressed. <br/>
    /// - Mouse5 and Mouse6 are not supported by InputSystem. <br/>
    /// - Some keys (such as Tilde) are not supported by InputSystem. See InputSystem API for more information. 
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns></returns>
    public static bool ConvertKeyCode(KeyCode key, out ButtonControl control)
    {
        control = default;

        if (key == KeyCode.None) return false;
        if (!keyboardPresent && IsAKeyboardKeycode(key)) return false;
        if (!mousePresent && IsAMouseKeycode(key)) return false;
        if (!gamepadPresent && IsAGamepadKeycode(key)) return false;

        switch (key)
        {
            default:
                return false;
            case KeyCode.A: control = Keyboard.current.aKey; break;
            case KeyCode.BackQuote: control = Keyboard.current.backquoteKey; break;
            case KeyCode.Backslash: control = Keyboard.current.backslashKey; break;
            case KeyCode.Backspace: control = Keyboard.current.backspaceKey; break;
            case KeyCode.B: control = Keyboard.current.bKey; break;
            case KeyCode.CapsLock: control = Keyboard.current.capsLockKey; break;
            case KeyCode.C: control = Keyboard.current.cKey; break;
            case KeyCode.Comma: control = Keyboard.current.commaKey; break;
            case KeyCode.Menu: control = Keyboard.current.contextMenuKey; break;
            case KeyCode.Delete: control = Keyboard.current.deleteKey; break;
            case KeyCode.Alpha0: control = Keyboard.current.digit0Key; break;
            case KeyCode.Alpha1: control = Keyboard.current.digit1Key; break;
            case KeyCode.Alpha2: control = Keyboard.current.digit2Key; break;
            case KeyCode.Alpha3: control = Keyboard.current.digit3Key; break;
            case KeyCode.Alpha4: control = Keyboard.current.digit4Key; break;
            case KeyCode.Alpha5: control = Keyboard.current.digit5Key; break;
            case KeyCode.Alpha6: control = Keyboard.current.digit6Key; break;
            case KeyCode.Alpha7: control = Keyboard.current.digit7Key; break;
            case KeyCode.Alpha8: control = Keyboard.current.digit8Key; break;
            case KeyCode.Alpha9: control = Keyboard.current.digit9Key; break;
            case KeyCode.D: control = Keyboard.current.dKey; break;
            case KeyCode.DownArrow: control = Keyboard.current.downArrowKey; break;
            case KeyCode.E: control = Keyboard.current.eKey; break;
            case KeyCode.End: control = Keyboard.current.endKey; break;
            case KeyCode.Return: control = Keyboard.current.enterKey; break;
            case KeyCode.Equals: control = Keyboard.current.equalsKey; break;
            case KeyCode.Escape: control = Keyboard.current.escapeKey; break;
            case KeyCode.F1: control = Keyboard.current.f1Key; break;
            case KeyCode.F2: control = Keyboard.current.f2Key; break;
            case KeyCode.F3: control = Keyboard.current.f3Key; break;
            case KeyCode.F4: control = Keyboard.current.f4Key; break;
            case KeyCode.F5: control = Keyboard.current.f5Key; break;
            case KeyCode.F6: control = Keyboard.current.f6Key; break;
            case KeyCode.F7: control = Keyboard.current.f7Key; break;
            case KeyCode.F8: control = Keyboard.current.f8Key; break;
            case KeyCode.F9: control = Keyboard.current.f9Key; break;
            case KeyCode.F10: control = Keyboard.current.f10Key; break;
            case KeyCode.F11: control = Keyboard.current.f11Key; break;
            case KeyCode.F12: control = Keyboard.current.f12Key; break;
            case KeyCode.F: control = Keyboard.current.fKey; break;
            case KeyCode.G: control = Keyboard.current.gKey; break;
            case KeyCode.H: control = Keyboard.current.hKey; break;
            case KeyCode.Home: control = Keyboard.current.homeKey; break;
            case KeyCode.I: control = Keyboard.current.iKey; break;
            case KeyCode.Insert: control = Keyboard.current.insertKey; break;
            case KeyCode.J: control = Keyboard.current.jKey; break;
            case KeyCode.K: control = Keyboard.current.kKey; break;
            case KeyCode.LeftAlt: control = Keyboard.current.leftAltKey; break;
            case KeyCode.LeftApple or KeyCode.LeftCommand or KeyCode.LeftMeta: control = Keyboard.current.leftAppleKey; break;
            case KeyCode.LeftArrow: control = Keyboard.current.leftArrowKey; break;
            case KeyCode.LeftBracket or KeyCode.LeftCurlyBracket: control = Keyboard.current.leftBracketKey; break;
            case KeyCode.LeftControl: control = Keyboard.current.leftCtrlKey; break;
            case KeyCode.LeftShift: control = Keyboard.current.leftShiftKey; break;
            case KeyCode.LeftWindows: control = Keyboard.current.leftWindowsKey; break;
            case KeyCode.L: control = Keyboard.current.lKey; break;
            case KeyCode.Minus: control = Keyboard.current.minusKey; break;
            case KeyCode.M: control = Keyboard.current.mKey; break;
            case KeyCode.N: control = Keyboard.current.nKey; break;
            case KeyCode.Numlock: control = Keyboard.current.numLockKey; break;
            case KeyCode.Keypad0: control = Keyboard.current.numpad0Key; break;
            case KeyCode.Keypad1: control = Keyboard.current.numpad1Key; break;
            case KeyCode.Keypad2: control = Keyboard.current.numpad2Key; break;
            case KeyCode.Keypad3: control = Keyboard.current.numpad3Key; break;
            case KeyCode.Keypad4: control = Keyboard.current.numpad4Key; break;
            case KeyCode.Keypad5: control = Keyboard.current.numpad5Key; break;
            case KeyCode.Keypad6: control = Keyboard.current.numpad6Key; break;
            case KeyCode.Keypad7: control = Keyboard.current.numpad7Key; break;
            case KeyCode.Keypad8: control = Keyboard.current.numpad8Key; break;
            case KeyCode.Keypad9: control = Keyboard.current.numpad9Key; break;
            case KeyCode.KeypadDivide: control = Keyboard.current.numpadDivideKey; break;
            case KeyCode.KeypadEnter: control = Keyboard.current.numpadEnterKey; break;
            case KeyCode.KeypadEquals: control = Keyboard.current.numpadEqualsKey; break;
            case KeyCode.KeypadMinus: control = Keyboard.current.numpadMinusKey; break;
            case KeyCode.KeypadMultiply: control = Keyboard.current.numpadMultiplyKey; break;
            case KeyCode.KeypadPeriod: control = Keyboard.current.numpadPeriodKey; break;
            case KeyCode.Plus: control = Keyboard.current.numpadPlusKey; break;
            case KeyCode.O: control = Keyboard.current.oKey; break;
            case KeyCode.PageDown: control = Keyboard.current.pageDownKey; break;
            case KeyCode.PageUp: control = Keyboard.current.pageUpKey; break;
            case KeyCode.Pause or KeyCode.Break: control = Keyboard.current.pauseKey; break;
            case KeyCode.Period: control = Keyboard.current.periodKey; break;
            case KeyCode.P: control = Keyboard.current.pKey; break;
            case KeyCode.Print: control = Keyboard.current.printScreenKey; break;                   // Not 100% verified to match. 'Tis a guess, sire.
            case KeyCode.Q: control = Keyboard.current.qKey; break;
            case KeyCode.Quote: control = Keyboard.current.quoteKey; break;
            case KeyCode.RightAlt or KeyCode.AltGr: control = Keyboard.current.rightAltKey; break;  // Right-Alt and Alt-Gr are considered to match by some sources.
            case KeyCode.RightApple or KeyCode.RightCommand or KeyCode.RightMeta: control = Keyboard.current.rightAppleKey; break;
            case KeyCode.RightArrow: control = Keyboard.current.rightArrowKey; break;
            case KeyCode.RightBracket or KeyCode.RightCurlyBracket: control = Keyboard.current.rightBracketKey; break;
            case KeyCode.RightControl: control = Keyboard.current.rightCtrlKey; break;
            case KeyCode.RightShift: control = Keyboard.current.rightShiftKey; break;
            case KeyCode.RightWindows: control = Keyboard.current.rightWindowsKey; break;
            case KeyCode.R: control = Keyboard.current.rKey; break;
            case KeyCode.ScrollLock: control = Keyboard.current.scrollLockKey; break;
            case KeyCode.Semicolon: control = Keyboard.current.semicolonKey; break;
            case KeyCode.S: control = Keyboard.current.sKey; break;
            case KeyCode.Slash: control = Keyboard.current.slashKey; break;
            case KeyCode.Space: control = Keyboard.current.spaceKey; break;
            case KeyCode.Tab: control = Keyboard.current.tabKey; break;
            case KeyCode.T: control = Keyboard.current.tKey; break;
            case KeyCode.U: control = Keyboard.current.uKey; break;
            case KeyCode.UpArrow: control = Keyboard.current.upArrowKey; break;
            case KeyCode.V: control = Keyboard.current.vKey; break;
            case KeyCode.W: control = Keyboard.current.wKey; break;
            case KeyCode.X: control = Keyboard.current.xKey; break;
            case KeyCode.Y: control = Keyboard.current.yKey; break;
            case KeyCode.Z: control = Keyboard.current.zKey; break;
            case KeyCode.Mouse0: control = Mouse.current.leftButton; break;
            case KeyCode.Mouse1: control = Mouse.current.rightButton; break;
            case KeyCode.Mouse2: control = Mouse.current.middleButton; break;
            case KeyCode.Mouse3: control = Mouse.current.backButton; break;
            case KeyCode.Mouse4: control = Mouse.current.forwardButton; break;

            case KeyCode.JoystickButton0 or KeyCode.Joystick1Button0 or KeyCode.Joystick2Button0 or KeyCode.Joystick3Button0 or KeyCode.Joystick4Button0 
            or KeyCode.Joystick5Button0 or KeyCode.Joystick6Button0 or KeyCode.Joystick7Button0 or KeyCode.Joystick8Button0:
                control = ((Gamepad)CurrentGamepad.device).leftStickButton; break;

            case KeyCode.JoystickButton1 or KeyCode.Joystick1Button1 or KeyCode.Joystick2Button1 or KeyCode.Joystick3Button1 or KeyCode.Joystick4Button1 
            or KeyCode.Joystick5Button1 or KeyCode.Joystick6Button1 or KeyCode.Joystick7Button1 or KeyCode.Joystick8Button1:
                control = ((Gamepad)CurrentGamepad.device).rightStickButton; break;

            case KeyCode.JoystickButton2 or KeyCode.Joystick1Button2 or KeyCode.Joystick2Button2 or KeyCode.Joystick3Button2 or KeyCode.Joystick4Button2
            or KeyCode.Joystick5Button2 or KeyCode.Joystick6Button2 or KeyCode.Joystick7Button2 or KeyCode.Joystick8Button2:
                control = ((Gamepad)CurrentGamepad.device).leftShoulder; break;

            case KeyCode.JoystickButton3 or KeyCode.Joystick1Button3 or KeyCode.Joystick2Button3 or KeyCode.Joystick3Button3 or KeyCode.Joystick4Button3
            or KeyCode.Joystick5Button3 or KeyCode.Joystick6Button3 or KeyCode.Joystick7Button3 or KeyCode.Joystick8Button3:
                control = ((Gamepad)CurrentGamepad.device).rightShoulder; break;

            case KeyCode.JoystickButton4 or KeyCode.Joystick1Button4 or KeyCode.Joystick2Button4 or KeyCode.Joystick3Button4 or KeyCode.Joystick4Button4
            or KeyCode.Joystick5Button4 or KeyCode.Joystick6Button4 or KeyCode.Joystick7Button4 or KeyCode.Joystick8Button4:
                control = ((Gamepad)CurrentGamepad.device).leftTrigger; break;

            case KeyCode.JoystickButton5 or KeyCode.Joystick1Button5 or KeyCode.Joystick2Button5 or KeyCode.Joystick3Button5 or KeyCode.Joystick4Button5
            or KeyCode.Joystick5Button5 or KeyCode.Joystick6Button5 or KeyCode.Joystick7Button5 or KeyCode.Joystick8Button5:
                control = ((Gamepad)CurrentGamepad.device).rightTrigger; break;

            case KeyCode.JoystickButton6 or KeyCode.Joystick1Button6 or KeyCode.Joystick2Button6 or KeyCode.Joystick3Button6 or KeyCode.Joystick4Button6
            or KeyCode.Joystick5Button6 or KeyCode.Joystick6Button6 or KeyCode.Joystick7Button6 or KeyCode.Joystick8Button6:
                control = ((Gamepad)CurrentGamepad.device).buttonNorth; break;

            case KeyCode.JoystickButton7 or KeyCode.Joystick1Button7 or KeyCode.Joystick2Button7 or KeyCode.Joystick3Button7 or KeyCode.Joystick4Button7
            or KeyCode.Joystick5Button7 or KeyCode.Joystick6Button7 or KeyCode.Joystick7Button7 or KeyCode.Joystick8Button7:
                control = ((Gamepad)CurrentGamepad.device).buttonSouth; break;

            case KeyCode.JoystickButton8 or KeyCode.Joystick1Button8 or KeyCode.Joystick2Button8 or KeyCode.Joystick3Button8 or KeyCode.Joystick4Button8
            or KeyCode.Joystick5Button8 or KeyCode.Joystick6Button8 or KeyCode.Joystick7Button8 or KeyCode.Joystick8Button8:
                control = ((Gamepad)CurrentGamepad.device).buttonEast; break;

            case KeyCode.JoystickButton9 or KeyCode.Joystick1Button9 or KeyCode.Joystick2Button9 or KeyCode.Joystick3Button9 or KeyCode.Joystick4Button9
            or KeyCode.Joystick5Button9 or KeyCode.Joystick6Button9 or KeyCode.Joystick7Button9 or KeyCode.Joystick8Button9:
                control = ((Gamepad)CurrentGamepad.device).buttonWest; break;

            case KeyCode.JoystickButton10 or KeyCode.Joystick1Button10 or KeyCode.Joystick2Button10 or KeyCode.Joystick3Button10 or KeyCode.Joystick4Button10
            or KeyCode.Joystick5Button10 or KeyCode.Joystick6Button10 or KeyCode.Joystick7Button10 or KeyCode.Joystick8Button10:
                control = ((Gamepad)CurrentGamepad.device).dpad.up; break;

            case KeyCode.JoystickButton11 or KeyCode.Joystick1Button11 or KeyCode.Joystick2Button11 or KeyCode.Joystick3Button11 or KeyCode.Joystick4Button11
            or KeyCode.Joystick5Button11 or KeyCode.Joystick6Button11 or KeyCode.Joystick7Button11 or KeyCode.Joystick8Button11:
                control = ((Gamepad)CurrentGamepad.device).dpad.down; break;

            case KeyCode.JoystickButton12 or KeyCode.Joystick1Button12 or KeyCode.Joystick2Button12 or KeyCode.Joystick3Button12 or KeyCode.Joystick4Button12
            or KeyCode.Joystick5Button12 or KeyCode.Joystick6Button12 or KeyCode.Joystick7Button12 or KeyCode.Joystick8Button12:
                control = ((Gamepad)CurrentGamepad.device).dpad.right; break;

            case KeyCode.JoystickButton13 or KeyCode.Joystick1Button13 or KeyCode.Joystick2Button13 or KeyCode.Joystick3Button13 or KeyCode.Joystick4Button13
            or KeyCode.Joystick5Button13 or KeyCode.Joystick6Button13 or KeyCode.Joystick7Button13 or KeyCode.Joystick8Button13:
                control = ((Gamepad)CurrentGamepad.device).dpad.left; break;

            case KeyCode.JoystickButton14 or KeyCode.Joystick1Button14 or KeyCode.Joystick2Button14 or KeyCode.Joystick3Button14 or KeyCode.Joystick4Button14
            or KeyCode.Joystick5Button14 or KeyCode.Joystick6Button14 or KeyCode.Joystick7Button14 or KeyCode.Joystick8Button14:
                control = ((Gamepad)CurrentGamepad.device).selectButton; break;

            case KeyCode.JoystickButton15 or KeyCode.Joystick1Button15 or KeyCode.Joystick2Button15 or KeyCode.Joystick3Button15 or KeyCode.Joystick4Button15
            or KeyCode.Joystick5Button15 or KeyCode.Joystick6Button15 or KeyCode.Joystick7Button15 or KeyCode.Joystick8Button15:
                control = ((Gamepad)CurrentGamepad.device).startButton; break;
        }

        return true;
    }

    /// <summary>
    /// Convert the GamepadControl enum value to a corresponding KeyCode value.
    /// </summary>
    /// <param name="control">The GamepadControl value to convert.</param>
    /// <returns></returns>
    public static KeyCode GamepadControlToKeyCode(GamepadControl control)
    {
        switch (control)
        {
            default: return KeyCode.None;
            case GamepadControl.LeftStickButton: return KeyCode.JoystickButton0;
            case GamepadControl.RightStickButton: return KeyCode.JoystickButton1;
            case GamepadControl.LeftShoulder: return KeyCode.JoystickButton2;
            case GamepadControl.RightShoulder: return KeyCode.JoystickButton3;
            case GamepadControl.LeftTrigger: return KeyCode.JoystickButton4;
            case GamepadControl.RightTrigger: return KeyCode.JoystickButton5;
            case GamepadControl.ButtonNorth: return KeyCode.JoystickButton6;
            case GamepadControl.ButtonSouth: return KeyCode.JoystickButton7;
            case GamepadControl.ButtonEast: return KeyCode.JoystickButton8;
            case GamepadControl.ButtonWest: return KeyCode.JoystickButton9;
            case GamepadControl.DPadUp: return KeyCode.JoystickButton10;
            case GamepadControl.DPadDown: return KeyCode.JoystickButton11;
            case GamepadControl.DPadLeft: return KeyCode.JoystickButton12;
            case GamepadControl.DPadRight: return KeyCode.JoystickButton13;
        }
    }
    #endregion

    /// <summary>
    /// Convert the provided GamepadControl to it's relavent input.
    /// </summary>
    /// <param name="control">The GamepadCotnrol value to query for the corresponding Gamepad Button.</param>
    /// <returns></returns>
    public static bool GamepadControlToInput(GamepadControl control)
    {
        switch (control)
        {
            default: return false;
            case GamepadControl.LeftStickButton: return LeftStickButton;
            case GamepadControl.RightStickButton: return RightStickButton;
            case GamepadControl.LeftShoulder: return LeftShoulder;
            case GamepadControl.RightShoulder: return RightShoulder;
            case GamepadControl.LeftTrigger: return LeftTrigger;
            case GamepadControl.RightTrigger: return RightTrigger;
            case GamepadControl.ButtonNorth: return ButtonNorth;
            case GamepadControl.ButtonSouth: return ButtonSouth;
            case GamepadControl.ButtonEast: return ButtonEast;
            case GamepadControl.ButtonWest: return ButtonWest;
            case GamepadControl.DPadUp: return DpadUp;
            case GamepadControl.DPadDown: return DpadDown;
            case GamepadControl.DPadLeft: return DpadLeft;
            case GamepadControl.DPadRight: return DpadRight;
        }
    }

    /// <summary>
    /// Convert the provided GamepadControl to a string name. <br/>
    /// Included to make string-building a directory easier in cases where developers have a Resources folder for Input Icons.
    /// </summary>
    /// <param name="control"></param>
    /// <returns></returns>
    public static string GamepadControlAsString(GamepadControl control)
    {
        switch (control)
        {
            default: return "N/A";
            case GamepadControl.LeftStickButton: return "LeftStickButton";
            case GamepadControl.RightStickButton: return "RightStickButton";
            case GamepadControl.LeftShoulder: return "LeftShoulder";
            case GamepadControl.RightShoulder: return "RightShoulder";
            case GamepadControl.LeftTrigger: return "LeftTrigger";
            case GamepadControl.RightTrigger: return "RightTrigger";
            case GamepadControl.ButtonNorth: return "ButtonNorth";
            case GamepadControl.ButtonSouth: return "ButtonSouth";
            case GamepadControl.ButtonEast: return "ButtonEast";
            case GamepadControl.ButtonWest: return "ButtonWest";
            case GamepadControl.DPadUp: return "DpadUp";
            case GamepadControl.DPadDown: return "DpadDown";
            case GamepadControl.DPadLeft: return "DpadLeft";
            case GamepadControl.DPadRight: return "DpadRight";
        }
    }

    /// <summary>
    /// A wrapper for InputSystem's Keyboard.SetIMECursorPosition. <br/>
    /// Refers to the current keyboard or does nothing if there isn't one.
    /// </summary>
    /// <remarks>
    /// Tracking the value for developers may be inconsistent or incorrect. <br/>
    /// If so, please submit an Issue on GitHub with an example project for testing.
    /// </remarks>
    /// <param name="position"></param>
    static void SetIMECursorPosition(Vector2 position)
    {
        if (!keyboardPresent) return;
        m_lastIMECursorPosition = position;
        Keyboard.current.SetIMECursorPosition(position);
    }

    /// <summary>
    /// A method bound to Keyboard.onIMECompositionChange that converts the IMECompositionString stored by a keyboard into a string.
    /// </summary>
    /// <param name="compositionString">The IMECompositionString object returned by InputSystem.LowLevel.</param>
    static void OnIMECompositionChange(IMECompositionString compositionString) => m_imeCompositionString = compositionString.ToString();

    /// <summary>
    /// A method bound to Keyboard.onTextInput that converts the keyboard input created this frame into a string that can be read from.
    /// </summary>
    /// <param name="character"></param>
    static void OnKeyboardTextInput(char character) => m_frameInputString = character.ToString();

    // INPUT MEMBERS
    // - Mouse Inputs

    /// <summary>
    /// The current mouse position in pixel coordinates. (Read-Only).
    /// </summary>
    public static Vector2 mousePosition
    {
        get
        {
            if (mousePresent && TryReadValue(Mouse.current.position, out Vector2 value)) return value;
            else return default;
        }
    }

    /// <summary>
    /// Get the Mouse Movement for this frame. <br/>
    /// As MouseDelta is polling the value from Mouse.current.delta, some inconsistencies may occur.
    /// </summary>
    public static Vector2 mouseDelta
    {
        get { return mousePresent ? Mouse.current.delta.TryReadValue(out Vector2 val) ? val : Vector2.zero : Vector2.zero; }
    }

    // - Accelerometer Data

    /// <summary>
    /// Last measured linear acceleration of a device in three-dimensional space. (Read Only)
    /// </summary>
    public static Vector3 acceleration => accelerometerPresent ? (Accelerometer.current.acceleration.TryReadValue(out Vector3 value) ? value : default) : default;

    // - Controller Inputs
    /// <summary>
    /// Get the Left Joystick Axes.
    /// </summary>
    public static Vector2 LeftStick
    {
        get
        {
            if (gamepadPresent && TryReadValue(((Gamepad)CurrentGamepad.device).leftStick, out Vector2 value)) return value;
            else return Vector2.zero;
        }
    }

    /// <summary>
    /// Get the Right Joystick Axes.
    /// </summary>
    public static Vector2 RightStick
    {
        get
        {
            if (gamepadPresent && TryReadValue(((Gamepad)CurrentGamepad.device).rightStick, out Vector2 value)) return value;
            else return Vector2.zero;
        }
    }

    /// <summary>
    /// Get the D-Pad Axes for the D-Pad buttons.
    /// </summary>
    /// <remarks>
    /// Alternatively, for a single button on the D-Pad, the following members exist: <br/>
    /// <see cref="DpadUp"/><br/><see cref="DpadDown"/><br/><see cref="DpadLeft"/><br/><see cref="DpadRight"/>
    /// </remarks>
    public static Vector2 Dpad 
    {
        get
        {
            if (gamepadPresent && TryReadValue(((Gamepad)CurrentGamepad.device).dpad, out Vector2 value)) return value;
            else return default;
        }
    }
    
    // - - Controller Buttons Pressed

    /// <summary>
    /// Get whether the Left Stick on the current gamepad was pressed. <br/><br/>
    /// </summary>
    public static bool LeftStickButton
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).leftStickButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Stick on the current gamepad was pressed. <br/><br/>
    /// </summary>
    public static bool RightStickButton
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).rightStickButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Left Shoulder on the current gamepad was pressed. <br/><br/>
    /// </summary>
    public static bool LeftShoulder
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).leftShoulder, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Shoulder on the current gamepad was pressed. <br/><br/>
    /// </summary>
    public static bool RightShoulder
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).rightShoulder, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Left Trigger on the current gamepad was pressed. <br/><br/>
    /// </summary>
    public static bool LeftTrigger
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).leftTrigger, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Trigger on the current gamepad was pressed. <br/><br/>
    /// </summary>
    public static bool RightTrigger
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).rightTrigger, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the North-facing button on the current gamepad was pressed. <br/><br/>
    /// For XBOX: Y<br/>
    /// For Playstation: Triangle<br/>
    /// For Switch: X<br/>
    /// </summary>
    public static bool ButtonNorth
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).buttonNorth, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the South-facing button on the current gamepad was pressed. <br/><br/>
    /// For XBOX: A<br/>
    /// For Playstation: Cross<br/>
    /// For Switch: B<br/>
    /// </summary>
    public static bool ButtonSouth
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).buttonSouth, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the East-facing button on the current gamepad was pressed. <br/><br/>
    /// For XBOX: B<br/>
    /// For Playstation: Circle<br/>
    /// For Switch: A<br/>
    /// </summary>
    public static bool ButtonEast
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).buttonEast, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the West-facing button on the current gamepad was pressed. <br/><br/>
    /// For XBOX: X<br/>
    /// For Playstation: Square<br/>
    /// For Switch: Y<br/>
    /// </summary>
    public static bool ButtonWest
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).buttonWest, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Up button on the current gamepad was pressed.
    /// </summary>
    public static bool DpadUp
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).dpad.up, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Down button on the current gamepad was pressed.
    /// </summary>
    public static bool DpadDown
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).dpad.down, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Right button on the current gamepad was pressed.
    /// </summary>
    public static bool DpadRight
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).dpad.right, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Left button on the current gamepad was pressed.
    /// </summary>
    public static bool DpadLeft
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).dpad.left, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Select button on the current gamepad was pressed.  <br></br>
    /// For XBOX: View<br/>
    /// For Playstation: Share<br/>
    /// For Switch: -<br/>
    /// </summary>
    public static bool Select
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).selectButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Start button on the current gamepad was pressed.  <br></br>
    /// For XBOX: Menu<br/>
    /// For Playstation: Options<br/>
    /// For Switch: +<br/>
    /// </summary>
    public static bool Start
    {
        get
        {
            if (gamepadPresent && TryGetPressed(((Gamepad)CurrentGamepad.device).startButton, out bool value)) return value;
            else return false;
        }
    }

    // - - Controller Buttons Released

    /// <summary>
    /// Get whether the Left Stick on the current gamepad was released. <br/><br/>
    /// </summary>
    public static bool LeftStickButtonReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).leftStickButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Stick on the current gamepad was released. <br/><br/>
    /// </summary>
    public static bool RightStickButtonReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).rightStickButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Left Shoulder on the current gamepad was released. <br/><br/>
    /// </summary>
    public static bool LeftShoulderReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).leftShoulder, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Shoulder on the current gamepad was released. <br/><br/>
    /// </summary>
    public static bool RightShoulderReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).rightShoulder, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Left Trigger on the current gamepad was released. <br/><br/>
    /// </summary>
    public static bool LeftTriggerReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).leftTrigger, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Trigger on the current gamepad was released. <br/><br/>
    /// </summary>
    public static bool RightTriggerReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).rightTrigger, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the North-facing button on the current gamepad was released. <br/><br/>
    /// For XBOX: Y<br/>
    /// For Playstation: Triangle<br/>
    /// For Switch: X<br/>
    /// </summary>
    public static bool ButtonNorthReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).buttonNorth, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the South-facing button on the current gamepad was released. <br/><br/>
    /// For XBOX: A<br/>
    /// For Playstation: Cross<br/>
    /// For Switch: B<br/>
    /// </summary>
    public static bool ButtonSouthReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).buttonSouth, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the East-facing button on the current gamepad was released. <br/><br/>
    /// For XBOX: B<br/>
    /// For Playstation: Circle<br/>
    /// For Switch: A<br/>
    /// </summary>
    public static bool ButtonEastReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).buttonEast, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the West-facing button on the current gamepad was released. <br/><br/>
    /// For XBOX: X<br/>
    /// For Playstation: Square<br/>
    /// For Switch: Y<br/>
    /// </summary>
    public static bool ButtonWestReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).buttonWest, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Up button on the current gamepad was released.
    /// </summary>
    public static bool DpadUpReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).dpad.up, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Down button on the current gamepad was released.
    /// </summary>
    public static bool DpadDownReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).dpad.down, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Right button on the current gamepad was released.
    /// </summary>
    public static bool DpadRightReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).dpad.right, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Left button on the current gamepad was released.
    /// </summary>
    public static bool DpadLeftReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).dpad.left, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Select button on the current gamepad was released.  <br></br>
    /// For XBOX: View<br/>
    /// For Playstation: Share<br/>
    /// For Switch: -<br/>
    /// </summary>
    public static bool SelectReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).selectButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Start button on the current gamepad was released.  <br></br>
    /// For XBOX: Menu<br/>
    /// For Playstation: Options<br/>
    /// For Switch: +<br/>
    /// </summary>
    public static bool StartReleased
    {
        get
        {
            if (gamepadPresent && TryGetReleased(((Gamepad)CurrentGamepad.device).startButton, out bool value)) return value;
            else return false;
        }
    }

    // - - Controller Buttons Held Down

    /// <summary>
    /// Get whether the Left Stick on the current gamepad is held down. <br/><br/>
    /// </summary>
    public static bool LeftStickButtonHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).leftStickButton, out bool value)) return value;
            else return false;
        }
    }
    
    /// <summary>
    /// Get whether the Right Stick on the current gamepad is held down. <br/><br/>
    /// </summary>
    public static bool RightStickButtonHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).rightStickButton, out bool value)) return value;
            else return false;
        }
    }
   
    /// <summary>
    /// Get whether the Left Shoulder on the current gamepad is held down. <br/><br/>
    /// </summary>
    public static bool LeftShoulderHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).leftShoulder, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Shoulder on the current gamepad is held down. <br/><br/>
    /// </summary>
    public static bool RightShoulderHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).rightShoulder, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Left Trigger on the current gamepad is held down. <br/><br/>
    /// </summary>
    public static bool LeftTriggerHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).leftTrigger, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Right Trigger on the current gamepad is held down. <br/><br/>
    /// </summary>
    public static bool RightTriggerHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).rightTrigger, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the North-facing button on the current gamepad is held down. <br/><br/>
    /// For XBOX: Y<br/>
    /// For Playstation: Triangle<br/>
    /// For Switch: X<br/>
    /// </summary>
    public static bool ButtonNorthHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).buttonNorth, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the South-facing button on the current gamepad is held down. <br/><br/>
    /// For XBOX: A<br/>
    /// For Playstation: Cross<br/>
    /// For Switch: B<br/>
    /// </summary>
    public static bool ButtonSouthHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).buttonSouth, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the East-facing button on the current gamepad is held down. <br/><br/>
    /// For XBOX: B<br/>
    /// For Playstation: Circle<br/>
    /// For Switch: A<br/>
    /// </summary>
    public static bool ButtonEastHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).buttonEast, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the West-facing button on the current gamepad is held down. <br/><br/>
    /// For XBOX: X<br/>
    /// For Playstation: Square<br/>
    /// For Switch: Y<br/>
    /// </summary>
    public static bool ButtonWestHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).buttonWest, out bool value)) return value;
            else return false;
        }
    }
    
    /// <summary>
    /// Get whether the D-Pad Up button on the current gamepad is held down.
    /// </summary>
    public static bool DpadUpHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).dpad.up, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Down button on the current gamepad is held down.
    /// </summary>
    public static bool DpadDownHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).dpad.down, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Right button on the current gamepad is held down.
    /// </summary>
    public static bool DpadRightHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).dpad.right, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the D-Pad Left button on the current gamepad is held down.
    /// </summary>
    public static bool DpadLeftHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).dpad.left, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Select button on the current gamepad is held down.  <br></br>
    /// For XBOX: View<br/>
    /// For Playstation: Share<br/>
    /// For Switch: -<br/>
    /// </summary>
    public static bool SelectHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).selectButton, out bool value)) return value;
            else return false;
        }
    }

    /// <summary>
    /// Get whether the Start button on the current gamepad is held down.  <br></br>
    /// For XBOX: Menu<br/>
    /// For Playstation: Options<br/>
    /// For Switch: +<br/>
    /// </summary>
    public static bool StartHeld
    {
        get
        {
            if (gamepadPresent && TryGetHeld(((Gamepad)CurrentGamepad.device).startButton, out bool value)) return value;
            else return false;
        }
    }

    // - - Controller Actuation Values (The current float representing how far a button is held down)

    /// <summary>
    /// Get how far down the Left Trigger is pressed. <br/><br/>
    /// </summary>
    public static float LeftTriggerActuation
    {
        get
        {
            if (gamepadPresent && TryReadValue(((Gamepad)CurrentGamepad.device).leftTrigger, out float value)) return value;
            else return 0f;
        }
    }

    /// <summary>
    /// Get how far down the Right Trigger is pressed. <br/><br/>
    /// </summary>
    public static float RightTriggerActuation
    {
        get
        {
            if (gamepadPresent && TryReadValue(((Gamepad)CurrentGamepad.device).rightTrigger, out float value)) return value;
            else return 0f;
        }
    }
}
#endregion