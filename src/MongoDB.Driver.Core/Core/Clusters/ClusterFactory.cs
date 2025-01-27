/* Copyright 2013-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver.Core.Clusters
{
    internal class ClusterFactory : IClusterFactory
    {
        // fields
        private readonly IEventSubscriber _eventSubscriber;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IClusterableServerFactory _serverFactory;
        private readonly ClusterSettings _settings;

        // constructors
        public ClusterFactory(ClusterSettings settings, IClusterableServerFactory serverFactory, IEventSubscriber eventSubscriber, ILoggerFactory loggerFactory)
        {
            _settings = Ensure.IsNotNull(settings, nameof(settings));
            _serverFactory = Ensure.IsNotNull(serverFactory, nameof(serverFactory));
            _eventSubscriber = Ensure.IsNotNull(eventSubscriber, nameof(eventSubscriber));
            _loggerFactory = loggerFactory;
        }

        // methods
        public ICluster CreateCluster()
        {
            var settings = _settings;

            bool createLoadBalancedCluster = settings.LoadBalanced;
            if (createLoadBalancedCluster)
            {
                return CreateLoadBalancedCluster(settings);
            }

            bool createSingleServerCluster;
#pragma warning disable CS0618 // Type or member is obsolete
            if (settings.ConnectionModeSwitch == ConnectionModeSwitch.UseDirectConnection)
            {
                createSingleServerCluster = settings.DirectConnection.GetValueOrDefault();
            }
            else
            {
                var connectionMode = settings.ConnectionMode;
                if (connectionMode == ClusterConnectionMode.Automatic)
                {
                    if (settings.ReplicaSetName != null)
                    {
                        connectionMode = ClusterConnectionMode.ReplicaSet;
                        settings = settings.With(connectionMode: connectionMode, connectionModeSwitch: ConnectionModeSwitch.UseConnectionMode); // update connectionMode
                    }
                }

                createSingleServerCluster =
                    connectionMode == ClusterConnectionMode.Direct ||
                    connectionMode == ClusterConnectionMode.Standalone ||
                    (
                        connectionMode == ClusterConnectionMode.Automatic &&
                        settings.EndPoints.Count == 1 &&
                        settings.Scheme != ConnectionStringScheme.MongoDBPlusSrv
                    );
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (createSingleServerCluster)
            {
                return CreateSingleServerCluster(settings);
            }
            else
            {
                return CreateMultiServerCluster(settings);
            }
        }

        private MultiServerCluster CreateMultiServerCluster(ClusterSettings settings)
        {
            return new MultiServerCluster(settings, _serverFactory, _eventSubscriber, _loggerFactory);
        }

        private SingleServerCluster CreateSingleServerCluster(ClusterSettings settings)
        {
            return new SingleServerCluster(settings, _serverFactory, _eventSubscriber, _loggerFactory);
        }

        private LoadBalancedCluster CreateLoadBalancedCluster(ClusterSettings setting)
        {
            return new LoadBalancedCluster(setting, _serverFactory, _eventSubscriber, _loggerFactory);
        }
    }
}
