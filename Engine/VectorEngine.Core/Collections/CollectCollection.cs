#region File Description
//-----------------------------------------------------------------------------
// CollectCollection.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using System.Collections;
#endregion

namespace VectorEngine.Collections
{
    /// <summary>
    /// A collection with an additional feature to isolate the "collection"
    /// of the objects.
    /// </summary>
    public sealed class CollectCollection<T>
        : System.Collections.ObjectModel.Collection<T>
    {
        #region Fields and Properties
        private System.Collections.ObjectModel.Collection<T> garbage;
        public System.Collections.ObjectModel.Collection<T> Garbage
        {
            get { return garbage; }
            set { garbage = value; }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Constructs a new collection.
        /// </summary>
        public CollectCollection()
        {
            this.garbage =
            new System.Collections.ObjectModel.Collection<T>();
        }
        #endregion

        #region Collection
        /// <summary>
        /// Remove all of the "garbage" objects from this collection.
        /// Optimized to O(n) by using backward iteration and RemoveAt instead of Remove.
        /// </summary>
        public void Collect()
        {
            if (garbage.Count == 0)
                return;

            // For small garbage counts, use HashSet for O(1) lookups
            // For large garbage counts (>50% of collection), just rebuild the list
            if (garbage.Count > Count / 2)
            {
                // Most items are garbage, faster to rebuild
                for (int i = Count - 1; i >= 0; i--)
                {
                    if (garbage.Contains(this[i]))
                    {
                        RemoveAt(i);
                    }
                }
            }
            else
            {
                // Few items are garbage, use HashSet for fast lookup
                var garbageSet = new System.Collections.Generic.HashSet<T>(garbage);
                for (int i = Count - 1; i >= 0; i--)
                {
                    if (garbageSet.Contains(this[i]))
                    {
                        RemoveAt(i);
                    }
                }
            }

            garbage.Clear();
        }
        #endregion
    }
}
