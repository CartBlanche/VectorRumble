﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace VectorRumble {
    using System;
    using System.Reflection;
    
    
    internal static class ResourceMapExt {
        public static string GetString (this Windows.ApplicationModel.Resources.Core.ResourceMap map, string s, object foo)
        {
            return map.GetValue(s).ValueAsString;
        }
    }
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    //[global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    //[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    //[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Strings {
        
        private static global::Windows.ApplicationModel.Resources.Core.ResourceMap resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Strings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::Windows.ApplicationModel.Resources.Core.ResourceMap ResourceManager {
            get {
                if (object.ReferenceEquals (resourceMan, null))
                {
                    resourceMan = Windows.ApplicationModel.Resources.Core.ResourceManager.Current.MainResourceMap.GetSubtree ("Strings");
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Are you sure?.
        /// </summary>
        internal static string Are_You_Sure {
            get {
                return ResourceManager.GetString("Are_You_Sure", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Asteroid Density : {0}.
        /// </summary>
        internal static string Asteroid_Density {
            get {
                return ResourceManager.GetString("Asteroid_Density", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to High.
        /// </summary>
        internal static string Asteroid_Density_High {
            get {
                return ResourceManager.GetString("Asteroid_Density_High", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Low.
        /// </summary>
        internal static string Asteroid_Density_Low {
            get {
                return ResourceManager.GetString("Asteroid_Density_Low", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Medium.
        /// </summary>
        internal static string Asteroid_Density_Medium {
            get {
                return ResourceManager.GetString("Asteroid_Density_Medium", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to None.
        /// </summary>
        internal static string Asteroid_Density_None {
            get {
                return ResourceManager.GetString("Asteroid_Density_None", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Blur Intesity : {0}.
        /// </summary>
        internal static string Blur_Intesity {
            get {
                return ResourceManager.GetString("Blur_Intesity", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connect  GamePad.
        /// </summary>
        internal static string Connect_Gamepad {
            get {
                return ResourceManager.GetString("Connect_Gamepad", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exit.
        /// </summary>
        internal static string Exit {
            get {
                return ResourceManager.GetString("Exit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exit VectorRumble?.
        /// </summary>
        internal static string Exit_Prompt {
            get {
                return ResourceManager.GetString("Exit_Prompt", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Hold  A  to  Join.
        /// </summary>
        internal static string Hold_A_To_Join {
            get {
                return ResourceManager.GetString("Hold_A_To_Join", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading....
        /// </summary>
        internal static string Loading {
            get {
                return ResourceManager.GetString("Loading", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A button, Space, Enter = ok
        ///B button, Esc = cancel.
        /// </summary>
        internal static string MessageBox_Usage {
            get {
                return ResourceManager.GetString("MessageBox_Usage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Motion Blur : {0}.
        /// </summary>
        internal static string Motion_Blur {
            get {
                return ResourceManager.GetString("Motion_Blur", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Neon Effect : {0}.
        /// </summary>
        internal static string Neon_Effect {
            get {
                return ResourceManager.GetString("Neon_Effect", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Options.
        /// </summary>
        internal static string Options {
            get {
                return ResourceManager.GetString("Options", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Play Again.
        /// </summary>
        internal static string Play_Again {
            get {
                return ResourceManager.GetString("Play_Again", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Play Game.
        /// </summary>
        internal static string Play_Game {
            get {
                return ResourceManager.GetString("Play Game", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Player {0} wins the game!.
        /// </summary>
        internal static string Player_X_Wins {
            get {
                return ResourceManager.GetString("Player_X_Wins", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Press  M  to  Join.
        /// </summary>
        internal static string Press_M_To_Join {
            get {
                return ResourceManager.GetString("Press_M_To_Join", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Press  Z  to  Join.
        /// </summary>
        internal static string Press_Z_To_Join {
            get {
                return ResourceManager.GetString("Press_Z_To_Join", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Quit Game.
        /// </summary>
        internal static string Quit_Game {
            get {
                return ResourceManager.GetString("Quit Game", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Resume Game.
        /// </summary>
        internal static string Resume_Game {
            get {
                return ResourceManager.GetString("Resume Game", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Return to Menu.
        /// </summary>
        internal static string Return_to_Menu {
            get {
                return ResourceManager.GetString("Return_to_Menu", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Player {0} : {1}.
        /// </summary>
        internal static string Score {
            get {
                return ResourceManager.GetString("Score", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Score Limit : {0}.
        /// </summary>
        internal static string Score_Limit {
            get {
                return ResourceManager.GetString("Score_Limit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wall Style : {0}.
        /// </summary>
        internal static string Wall_Style {
            get {
                return ResourceManager.GetString("Wall_Style", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to None.
        /// </summary>
        internal static string Wall_Style_None {
            get {
                return ResourceManager.GetString("Wall_Style_None", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to One.
        /// </summary>
        internal static string Wall_Style_One {
            get {
                return ResourceManager.GetString("Wall_Style_One", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Random.
        /// </summary>
        internal static string Wall_Style_Random {
            get {
                return ResourceManager.GetString("Wall_Style_Random", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Three.
        /// </summary>
        internal static string Wall_Style_Three {
            get {
                return ResourceManager.GetString("Wall_Style_Three", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Two.
        /// </summary>
        internal static string Wall_Style_Two {
            get {
                return ResourceManager.GetString("Wall_Style_Two", resourceCulture);
            }
        }
    }
}
