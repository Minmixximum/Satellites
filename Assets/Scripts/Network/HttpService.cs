using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SatelliteEdgeComputing.Network
{
    /// <summary>
    /// HTTP服务基础类
    /// </summary>
    public class HttpService : MonoBehaviour
    {
        [Header("HTTP配置")]
        [SerializeField] protected string baseUrl = "http://localhost:5000";
        [SerializeField] protected float defaultTimeout = 10f;
        [SerializeField] protected int maxRetries = 3;
        [SerializeField] protected float retryDelay = 1f;

        [Header("请求头")]
        [SerializeField] protected Dictionary<string, string> defaultHeaders = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
            { "Accept", "application/json" }
        };

        [Header("状态")]
        [SerializeField] protected bool isConnected = false;
        [SerializeField] protected string lastError = "";

        // 事件
        public event Action<bool> OnConnectionStatusChanged;
        public event Action<string, string> OnRequestCompleted;
        public event Action<string, string> OnRequestFailed;

        /// <summary>
        /// 发送GET请求
        /// </summary>
        public virtual IEnumerator Get(string endpoint, Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);
            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null, successCallback, errorCallback);
        }

        /// <summary>
        /// 发送POST请求
        /// </summary>
        public virtual IEnumerator Post(string endpoint, string jsonData, Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);
            yield return SendRequest(url, UnityWebRequest.kHttpVerbPOST, jsonData, successCallback, errorCallback);
        }

        /// <summary>
        /// 发送PUT请求
        /// </summary>
        public virtual IEnumerator Put(string endpoint, string jsonData, Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);
            yield return SendRequest(url, UnityWebRequest.kHttpVerbPUT, jsonData, successCallback, errorCallback);
        }

        /// <summary>
        /// 发送DELETE请求
        /// </summary>
        public virtual IEnumerator Delete(string endpoint, Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);
            yield return SendRequest(url, UnityWebRequest.kHttpVerbDELETE, null, successCallback, errorCallback);
        }

        /// <summary>
        /// 发送通用HTTP请求（支持重试）
        /// </summary>
        protected virtual IEnumerator SendRequest(string url, string method, string jsonData,
            Action<string> successCallback, Action<string> errorCallback)
        {
            int retryCount = 0;
            bool success = false;
            string responseText = "";
            string error = "";

            while (retryCount < maxRetries && !success)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, method))
                {
                    request.timeout = (int)defaultTimeout;
                    request.downloadHandler = new DownloadHandlerBuffer();

                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
                    }

                    // 设置默认请求头
                    foreach (var header in defaultHeaders)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        success = true;
                        responseText = request.downloadHandler.text;
                        lastError = "";
                        UpdateConnectionStatus(true);

                        OnRequestCompleted?.Invoke(url, responseText);
                        successCallback?.Invoke(responseText);
                    }
                    else
                    {
                        error = $"{method} {url} 失败 ({retryCount + 1}/{maxRetries}): {request.error}";
                        lastError = error;
                        retryCount++;

                        if (retryCount < maxRetries)
                        {
                            UpdateConnectionStatus(false);
                            OnRequestFailed?.Invoke(url, error);
                            Debug.LogWarning($"请求失败，{retryDelay}秒后重试... ({retryCount}/{maxRetries})");
                            yield return new WaitForSeconds(retryDelay);
                        }
                        else
                        {
                            UpdateConnectionStatus(false);
                            OnRequestFailed?.Invoke(url, error);
                            errorCallback?.Invoke(error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        public virtual IEnumerator TestConnection(Action<bool> callback = null)
        {
            string testEndpoint = "/api/health";
            bool connected = false;

            yield return Get(testEndpoint,
                (response) =>
                {
                    connected = true;
                    UpdateConnectionStatus(true);
                    callback?.Invoke(true);
                },
                (error) =>
                {
                    connected = false;
                    UpdateConnectionStatus(false);
                    callback?.Invoke(false);
                });

            // 如果没有/status端点，尝试获取根路径
            if (!connected)
            {
                yield return Get("",
                    (response) =>
                    {
                        UpdateConnectionStatus(true);
                        callback?.Invoke(true);
                    },
                    (error) =>
                    {
                        UpdateConnectionStatus(false);
                        callback?.Invoke(false);
                    });
            }
        }

        /// <summary>
        /// 更新连接状态
        /// </summary>
        protected virtual void UpdateConnectionStatus(bool connected)
        {
            if (isConnected != connected)
            {
                isConnected = connected;
                OnConnectionStatusChanged?.Invoke(connected);
            }
        }

        /// <summary>
        /// 组合URL
        /// </summary>
        protected virtual string CombineUrl(string baseUrl, string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                return baseUrl;

            if (endpoint.StartsWith("/"))
                endpoint = endpoint.Substring(1);

            if (baseUrl.EndsWith("/"))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);

            return $"{baseUrl}/{endpoint}";
        }

        /// <summary>
        /// 添加默认请求头
        /// </summary>
        public virtual void AddDefaultHeader(string key, string value)
        {
            if (defaultHeaders.ContainsKey(key))
            {
                defaultHeaders[key] = value;
            }
            else
            {
                defaultHeaders.Add(key, value);
            }
        }

        /// <summary>
        /// 移除默认请求头
        /// </summary>
        public virtual void RemoveDefaultHeader(string key)
        {
            if (defaultHeaders.ContainsKey(key))
            {
                defaultHeaders.Remove(key);
            }
        }

        /// <summary>
        /// 设置超时时间
        /// </summary>
        public virtual void SetTimeout(float timeout)
        {
            defaultTimeout = Mathf.Max(1f, timeout);
        }

        /// <summary>
        /// 设置最大重试次数
        /// </summary>
        public virtual void SetMaxRetries(int retries)
        {
            maxRetries = Mathf.Max(0, retries);
        }

        /// <summary>
        /// 设置重试延迟
        /// </summary>
        public virtual void SetRetryDelay(float delay)
        {
            retryDelay = Mathf.Max(0.1f, delay);
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public virtual bool IsConnected()
        {
            return isConnected;
        }

        /// <summary>
        /// 获取最后错误信息
        /// </summary>
        public virtual string GetLastError()
        {
            return lastError;
        }

        /// <summary>
        /// 获取基本URL
        /// </summary>
        public virtual string GetBaseUrl()
        {
            return baseUrl;
        }

        /// <summary>
        /// 设置基本URL
        /// </summary>
        public virtual void SetBaseUrl(string url)
        {
            baseUrl = url;
        }

        /// <summary>
        /// 解析JSON响应
        /// </summary>
        protected virtual T ParseJsonResponse<T>(string json)
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON解析失败: {e.Message}\nJSON内容: {json}");
                throw;
            }
        }

        /// <summary>
        /// 创建查询字符串
        /// </summary>
        protected virtual string CreateQueryString(Dictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return "";

            List<string> queryParts = new List<string>();
            foreach (var param in parameters)
            {
                string key = UnityEngine.Networking.UnityWebRequest.EscapeURL(param.Key);
                string value = UnityEngine.Networking.UnityWebRequest.EscapeURL(param.Value);
                queryParts.Add($"{key}={value}");
            }

            return "?" + string.Join("&", queryParts);
        }

        /// <summary>
        /// 发送带查询参数的GET请求
        /// </summary>
        public virtual IEnumerator GetWithQuery(string endpoint, Dictionary<string, string> queryParams,
            Action<string> successCallback, Action<string> errorCallback = null)
        {
            string queryString = CreateQueryString(queryParams);
            string url = CombineUrl(baseUrl, endpoint) + queryString;
            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null, successCallback, errorCallback);
        }

        /// <summary>
        /// 发送表单数据
        /// </summary>
        public virtual IEnumerator PostForm(string endpoint, WWWForm form,
            Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);

            using (UnityWebRequest request = UnityWebRequest.Post(url, form))
            {
                request.timeout = (int)defaultTimeout;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    successCallback?.Invoke(responseText);
                    OnRequestCompleted?.Invoke(url, responseText);
                }
                else
                {
                    string error = $"POST表单 {url} 失败: {request.error}";
                    errorCallback?.Invoke(error);
                    OnRequestFailed?.Invoke(url, error);
                }
            }
        }

        /// <summary>
        /// 发送文件上传请求
        /// </summary>
        public virtual IEnumerator UploadFile(string endpoint, byte[] fileData, string fileName,
            Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection("file", fileData, fileName, "application/octet-stream"));

            using (UnityWebRequest request = UnityWebRequest.Post(url, formData))
            {
                request.timeout = (int)defaultTimeout;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    successCallback?.Invoke(responseText);
                    OnRequestCompleted?.Invoke(url, responseText);
                }
                else
                {
                    string error = $"文件上传 {url} 失败: {request.error}";
                    errorCallback?.Invoke(error);
                    OnRequestFailed?.Invoke(url, error);
                }
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        public virtual IEnumerator DownloadFile(string endpoint, string savePath,
            Action<string> successCallback, Action<string> errorCallback = null)
        {
            string url = CombineUrl(baseUrl, endpoint);

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)defaultTimeout;
                request.downloadHandler = new DownloadHandlerFile(savePath);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    successCallback?.Invoke(savePath);
                    OnRequestCompleted?.Invoke(url, $"文件已保存到: {savePath}");
                }
                else
                {
                    string error = $"文件下载 {url} 失败: {request.error}";
                    errorCallback?.Invoke(error);
                    OnRequestFailed?.Invoke(url, error);
                }
            }
        }
    }
}
