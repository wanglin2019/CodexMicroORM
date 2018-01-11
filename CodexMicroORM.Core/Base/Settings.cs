﻿/***********************************************************************
Copyright 2017 CodeX Enterprises LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Major Changes:
12/2017    0.2     Initial release (Joel Champagne)
***********************************************************************/
using System;
using System.Collections.Generic;
using System.Data;
using CodexMicroORM.Core.Services;

namespace CodexMicroORM.Core
{
    /// <summary>
    /// Settings applicable to service scopes.
    /// </summary>
    public sealed class ServiceScopeSettings
    {
        public bool InitializeNullCollections
        {
            get;
            set;
        } = Globals.DefaultInitializeNullCollections;

        public SerializationMode SerializationMode
        {
            get;
            set;
        } = Globals.DefaultSerializationMode;

        public MergeBehavior MergeBehavior
        {
            get;
            set;
        } = Globals.DefaultMergeBehavior;

        public Func<string> GetLastUpdatedBy
        {
            get;
            set;
        } = AuditService.GetLastUpdatedBy;

        [ThreadStatic]
        public bool CanDispose = true;
    }

    /// <summary>
    /// Settings applicable to connection scopes.
    /// </summary>
    public sealed class ConnectionScopeSettings
    {
        public ScopeMode? ScopeMode
        {
            get;
            set;
        } = null;

        public string ConnectionStringOverride
        {
            get;
            set;
        }

        public bool? IsTransactional
        {
            get;
            set;
        } = null;
    }

    /// <summary>
    /// Settings applicable to the request to save to the database.
    /// </summary>
    public sealed class DBSaveSettings
    {
        public int MaxDegreeOfParallelism
        {
            get;
            set;
        } = Globals.DefaultDBSaveDOP;

        [Flags]
        public enum Operations
        {
            Insert = 1,
            Update = 2,
            Delete = 3,
            All = 7
        }

        public object RootObject
        {
            get;
            set;
        } = null;

        public Operations AllowedOperations
        {
            get;
            set;
        } = Operations.All;

        public BulkRules BulkInsertRules
        {
            get;
            set;
        } = Globals.DefaultBulkInsertRules;

        private IList<Type> _bulkInsertTypes = new List<Type>();

        public IList<Type> BulkInsertTypes
        {
            get
            {
                return _bulkInsertTypes;
            }
            set
            {
                if (value?.Count > 0)
                {
                    BulkInsertRules = BulkRules.ByType;
                }

                _bulkInsertTypes = value;
            }
        }

        public int BulkInsertMinimumRows
        {
            get;
            set;
        } = 100000;

        public DBSaveSettings UseBulkInsertTypes(params Type[] types)
        {
            BulkInsertTypes = types;
            return this;
        }

        public Type IgnoreObjectType
        {
            get;
            set;
        } = null;

        public IEnumerable<object> SourceList
        {
            get;
            set;
        } = null;

        public Func<ICEFInfraWrapper, (bool cansave, ObjectState? treatas)> RowSavePreview
        {
            get;
            set;
        }
    }

}
