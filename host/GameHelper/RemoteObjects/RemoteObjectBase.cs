// <copyright file="RemoteObjectBase.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Utils;

    /// <summary>
    ///     Points to a Memory location and reads/understands all the data from there.
    ///     CurrentAreaInstance in remote memory location changes w.r.t time or event. Due to this,
    ///     each remote memory object requires to implement a time/event based coroutine.
    /// </summary>
    public abstract class RemoteObjectBase
    {
        private readonly bool forceUpdate;
        private readonly object updateLock = new();
        private readonly object updateExecutionLock = new();
        private IntPtr address;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RemoteObjectBase" /> class.
        /// </summary>
        internal RemoteObjectBase()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RemoteObjectBase" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        /// <param name="forceUpdate">
        ///     True in case the object should be updated even if address hasn't changed.
        /// </param>
        /// <param name="skipFirstUpdate">
        ///      Pass true if derived object takes the responsibility of executing the <see cref="UpdateData"/>
        ///      in constructor. This fixes the issue where derived object is introducing a new dependency
        ///      and that dependency isn't initialized until the derived object constructor is executed.
        /// </param>
        internal RemoteObjectBase(IntPtr address, bool forceUpdate = false, bool skipFirstUpdate = false)
        {
            this.forceUpdate = forceUpdate;
            if (skipFirstUpdate)
            {
                this.address = address;
            }
            else
            {
                this.Address = address;
            }
        }

        /// <summary>
        ///     Gets or sets the address of the memory location.
        /// </summary>
        public IntPtr Address
        {
            get
            {
                lock (this.updateLock)
                {
                    return this.address;
                }
            }

            set
            {
                // Entity-map traversal invokes callbacks in parallel and can occasionally yield the
                // same remote object to two workers. Serialize the complete mutation for this object
                // so derived UpdateData implementations never update their Dictionary/HashSet state
                // concurrently. Address.get remains independent and is only held for the pointer copy.
                lock (this.updateExecutionLock)
                {
                    bool hasAddressChanged;
                    lock (this.updateLock)
                    {
                        hasAddressChanged = this.address != value;
                        if (!(hasAddressChanged || this.forceUpdate))
                        {
                            return;
                        }

                        this.address = value;
                    }

                    // F-095: UpdateData/CleanUpData run outside updateLock, so a potentially long
                    // memory-read sequence does not block Address.get. updateExecutionLock only
                    // serializes writers for this instance. Derived exceptions remain contained.
                    try
                    {
                        if (value == IntPtr.Zero)
                        {
                            this.CleanUpData();
                        }
                        else
                        {
                            this.UpdateData(hasAddressChanged);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{this.GetType().Name}.Address.set] {ex}");
                    }
                }
            }
        }

        /// <summary>
        ///     Reads the memory and update all the data known by this Object.
        /// </summary>
        /// <param name="hasAddressChanged">
        ///     true in case the address has changed; otherwise false.
        /// </param>
        protected abstract void UpdateData(bool hasAddressChanged);

        /// <summary>
        ///     Knows how to clean up the object.
        /// </summary>
        protected abstract void CleanUpData();
    }
}
