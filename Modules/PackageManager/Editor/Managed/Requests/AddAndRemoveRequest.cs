// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Linq;
using UnityEngine;

namespace UnityEditor.PackageManager.Requests
{
    [Serializable]
    public sealed partial class AddAndRemoveRequest : Request<PackageCollection>
    {
        /// <summary>
        /// Constructor to support serialization
        /// </summary>
        private AddAndRemoveRequest()
        {
        }

        internal AddAndRemoveRequest(long operationId, NativeStatusCode initialStatus)
            : base(operationId, initialStatus)
        {
        }

        protected override PackageCollection GetResult()
        {
            var operationStatus = GetOperationData(Id);
            var packageList = operationStatus.packageList.Where(p => p.type != ShimPackageType);
            return new PackageCollection(packageList, operationStatus.error);
        }
    }
}
