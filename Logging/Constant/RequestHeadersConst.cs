namespace Logging.Constant;

public static class RequestHeadersConst
{
    public const string RequestId = "X-Kong-Request-Id";
    public const string XForwardedFor = "X-Forwarded-For"; //To get proxy client ip
    public const string KongResponseLatency = "X-Kong-Response-Latency";
    public const string ServiceOrigin = "Server";
    public const string UserInfo = "UserInfo";
    public const string SkipUserInfo = "SkipUserInfo";
    public const string UserId = "UserId";
    public const string CacheEnabled = "X-Cache-Enabled";
}
