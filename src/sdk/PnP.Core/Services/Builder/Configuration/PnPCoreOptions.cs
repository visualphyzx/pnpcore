﻿using System;
using System.Collections.Generic;

namespace PnP.Core.Services.Builder.Configuration
{
    /// <summary>
    /// Options for configuring PnP Core SDK
    /// </summary>
    public class PnPCoreOptions 
    {
        /// <summary>
        /// Turns on/off telemetry, can be customized via configuration. Defaults to false.
        /// </summary>
        public bool DisableTelemetry { get; set; }

        /// <summary>
        /// AAD tenant id, used for telemetry purposes. Can be customized via configuration
        /// </summary>
        public Guid AADTenantId { get; set; }

        /// <summary>
        /// The global HTTP requests settings
        /// </summary>
        public PnPCoreHttpRequestsOptions HttpRequests { get; set; }

        /// <summary>
        /// The global PnPContext options
        /// </summary>
        public PnPCoreContextOptions PnPContext { get; set; }

        /// <summary>
        /// The sites options
        /// </summary>
        public PnPCoreSitesOptions Sites { get; } = new PnPCoreSitesOptions();

        /// <summary>
        /// The default Authentication Provider for the sites
        /// </summary>
        public IAuthenticationProvider DefaultAuthenticationProvider { get; set; }
    }

    /// <summary>
    /// Http request global settings
    /// </summary>
    public class PnPCoreHttpRequestsOptions
    {
        /// <summary>
        /// User agent value, can be customized via configuration 
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// SharePoint Online REST options
        /// </summary>
        public PnPCoreHttpRequestsSharePointRestOptions SharePointRest { get; set; }

        /// <summary>
        /// Microsoft Graph REST options
        /// </summary>
        public PnPCoreHttpRequestsGraphOptions MicrosoftGraph { get; set; }
    }

    /// <summary>
    /// SharePoint Online REST options
    /// </summary>
    public class PnPCoreHttpRequestsSharePointRestOptions
    {
        /// <summary>
        /// Use the Retry-After header for calculating the delay in case of a retry. Defaults to false
        /// </summary>
        public bool UseRetryAfterHeader { get; set; }

        /// <summary>
        /// When not using retry-after, how many times can a retry be made. Defaults to 10
        /// </summary>
        public int MaxRetries { get; set; } = 10;

        /// <summary>
        /// How many seconds to wait for the next retry attempt. Defaults to 3
        /// </summary>
        public int DelayInSeconds { get; set; } = 3;

        /// <summary>
        /// Use an incremental strategy for the delay: each retry doubles the previous delay time. Defaults to true
        /// </summary>
        public bool UseIncrementalDelay { get; set; } = true;

    }

    /// <summary>
    /// Microsoft Graph REST options
    /// </summary>
    public class PnPCoreHttpRequestsGraphOptions
    {
        /// <summary>
        /// Use the Retry-After header for calculating the delay in case of a retry. Defaults to true
        /// </summary>
        public bool UseRetryAfterHeader { get; set; } = true;

        /// <summary>
        /// When not using retry-after, how many times can a retry be made. Defaults to 10
        /// </summary>
        public int MaxRetries { get; set; } = 10;

        /// <summary>
        /// How many seconds to wait for the next retry attempt. Defaults to 3
        /// </summary>
        public int DelayInSeconds { get; set; } = 3;

        /// <summary>
        /// Use an incremental strategy for the delay: each retry doubles the previous delay time. Defaults to true
        /// </summary>
        public bool UseIncrementalDelay { get; set; } = true;
    }

    /// <summary>
    /// Microsoft Graph global settings
    /// </summary>
    public class PnPCoreContextOptions
    {
        /// <summary>
        /// Controls whether the library will try to use Microsoft Graph over REST whenever that's defined in the model
        /// </summary>
        public bool GraphFirst { get; set; } = true;

        /// <summary>
        /// If true than the Graph beta endpoint is used when there's no other option, default approach stays using the v1 endpoint
        /// </summary>
        public bool GraphCanUseBeta { get; set; } = true;

        /// <summary>
        /// If true than all requests to Microsoft Graph use the beta endpoint
        /// </summary>
        public bool GraphAlwaysUseBeta { get; set; }
    }

    /// <summary>
    /// Options for configuring PnP Core SDK
    /// </summary>
    public class PnPCoreSitesOptions : Dictionary<string, PnPCoreSiteOptions>
    {
    }

    /// <summary>
    /// Options for configuring a single site in PnP Core SDK
    /// </summary>
    public class PnPCoreSiteOptions
    {
        /// <summary>
        /// The URL of the target site
        /// </summary>
        public string SiteUrl { get; set; }

        /// <summary>
        /// The Authentication Provider
        /// </summary>
        public IAuthenticationProvider AuthenticationProvider { get; set; }
    }
}
