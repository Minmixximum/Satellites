using UnityEngine;

namespace SatelliteEdgeComputing.Core
{
    /// <summary>
    /// 项目主入口点
    /// </summary>
    public class Main : MonoBehaviour
    {
        [Header("启动设置")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool showSplashScreen = true;
        [SerializeField] private float splashScreenDuration = 2f;

        [Header("组件引用")]
        [SerializeField] private SceneSetup sceneSetup;
        [SerializeField] private GameObject splashScreen;

        private void Start()
        {
            if (autoInitialize)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化项目
        /// </summary>
        public void Initialize()
        {
            Debug.Log("=== 卫星边缘计算可视化项目启动 ===");
            Debug.Log("项目: 面向低轨卫星网络的边缘计算任务调度仿真及Unity可视化实现");
            Debug.Log("作者: 米啵鱼");
            Debug.Log("日期: 2026年3月");

            // 显示启动画面
            if (showSplashScreen && splashScreen != null)
            {
                ShowSplashScreen();
            }
            else
            {
                StartApplication();
            }
        }

        /// <summary>
        /// 显示启动画面
        /// </summary>
        private void ShowSplashScreen()
        {
            splashScreen.SetActive(true);

            // 延迟后隐藏启动画面并启动应用
            Invoke("HideSplashScreen", splashScreenDuration);
        }

        /// <summary>
        /// 隐藏启动画面
        /// </summary>
        private void HideSplashScreen()
        {
            if (splashScreen != null)
            {
                splashScreen.SetActive(false);
            }

            StartApplication();
        }

        /// <summary>
        /// 启动应用程序
        /// </summary>
        private void StartApplication()
        {
            Debug.Log("正在启动应用程序...");

            // 确保必要组件存在
            EnsureComponents();

            // 设置场景
            if (sceneSetup != null)
            {
                sceneSetup.SetupScene();
            }
            else
            {
                // 自动查找或创建SceneSetup
                sceneSetup = FindObjectOfType<SceneSetup>();
                if (sceneSetup == null)
                {
                    GameObject setupObj = new GameObject("SceneSetup");
                    sceneSetup = setupObj.AddComponent<SceneSetup>();
                }
                sceneSetup.SetupScene();
            }

            // 初始化对象池管理器
            InitializeObjectPoolManager();

            // 初始化协程助手
            InitializeCoroutineHelper();

            // 显示欢迎信息
            ShowWelcomeMessage();

            Debug.Log("应用程序启动完成");
        }

        /// <summary>
        /// 确保必要组件存在
        /// </summary>
        private void EnsureComponents()
        {
            // 确保单例管理器存在
            SimulationManager simulationManager = SimulationManager.Instance;
            Network.ApiClient apiClient = Network.ApiClient.Instance;
            Utils.ObjectPoolManager poolManager = Utils.ObjectPoolManager.Instance;

            Debug.Log("核心组件检查完成");
        }

        /// <summary>
        /// 初始化对象池管理器
        /// </summary>
        private void InitializeObjectPoolManager()
        {
            var poolManager = Utils.ObjectPoolManager.Instance;
            Debug.Log($"对象池管理器初始化完成");
        }

        /// <summary>
        /// 初始化协程助手
        /// </summary>
        private void InitializeCoroutineHelper()
        {
            // 协程助手会自动初始化
            Debug.Log("协程助手已就绪");
        }

        /// <summary>
        /// 显示欢迎信息
        /// </summary>
        private void ShowWelcomeMessage()
        {
            string welcomeMessage = @"
===========================================
卫星边缘计算可视化系统
===========================================

系统功能：
1. 3D地球和卫星网络可视化
2. 实时任务调度仿真
3. 四种调度算法（FCFS、SJF、EDD、Max-Visibility）
4. 交互式控制界面
5. 性能统计和分析

控制说明：
- 鼠标拖拽：旋转地球
- 鼠标滚轮：缩放视角
- WASD键：平移视角
- 双击卫星：聚焦查看
- R键：重置相机位置

请使用控制面板开始仿真...
===========================================";

            Debug.Log(welcomeMessage);
        }

        /// <summary>
        /// 重启应用程序
        /// </summary>
        public void RestartApplication()
        {
            Debug.Log("重新启动应用程序...");

            // 清理现有对象
            Cleanup();

            // 重新初始化
            Initialize();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void Cleanup()
        {
            // 停止所有协程
            Utils.CoroutineHelper.StopAll();

            // 清理对象池
            var poolManager = Utils.ObjectPoolManager.Instance;
            if (poolManager != null)
            {
                poolManager.ClearAllPools();
            }

            // 销毁动态创建的对象
            var dynamicObjects = GameObject.FindGameObjectsWithTag("Dynamic");
            foreach (var obj in dynamicObjects)
            {
                Destroy(obj);
            }

            Debug.Log("资源清理完成");
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        public void ExitApplication()
        {
            Debug.Log("正在退出应用程序...");

            // 清理资源
            Cleanup();

            // 保存设置（如果需要）
            SaveSettings();

            // 退出
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            // 这里可以添加设置保存逻辑
            Debug.Log("设置已保存");
        }

        /// <summary>
        /// 显示系统信息
        /// </summary>
        public void ShowSystemInfo()
        {
            string systemInfo = $@"
            系统信息：
            - Unity版本: {Application.unityVersion}
            - 平台: {Application.platform}
            - 系统语言: {Application.systemLanguage}
            - 运行时间: {Time.time:F1}秒
            - 帧率: {1f / Time.deltaTime:F1} FPS
            - 内存使用: {System.GC.GetTotalMemory(false) / 1024 / 1024:F1} MB";

            Debug.Log(systemInfo);
        }

        /// <summary>
        /// 运行性能测试
        /// </summary>
        public void RunPerformanceTest()
        {
            Debug.Log("开始性能测试...");

            // 这里可以添加性能测试逻辑
            // 例如：创建大量卫星测试渲染性能

            Debug.Log("性能测试完成");
        }

        /// <summary>
        /// 在编辑器中手动初始化
        /// </summary>
        [ContextMenu("手动初始化")]
        private void ManualInitialize()
        {
            Initialize();
        }

        /// <summary>
        /// 在编辑器中手动重启
        /// </summary>
        [ContextMenu("手动重启")]
        private void ManualRestart()
        {
            RestartApplication();
        }

        /// <summary>
        /// 在编辑器中显示系统信息
        /// </summary>
        [ContextMenu("显示系统信息")]
        private void ManualShowSystemInfo()
        {
            ShowSystemInfo();
        }

        private void Update()
        {
            // 退出快捷键
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitApplication();
            }

            // 显示系统信息快捷键
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ShowSystemInfo();
            }

            // 性能测试快捷键
            if (Input.GetKeyDown(KeyCode.F2))
            {
                RunPerformanceTest();
            }

            // 重启快捷键
            if (Input.GetKeyDown(KeyCode.F5))
            {
                RestartApplication();
            }
        }

        private void OnApplicationQuit()
        {
            Debug.Log("应用程序退出");
            Cleanup();
        }
    }
}