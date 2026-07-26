// Заглушки Unity API — ТОЛЬКО для локальной проверки компиляции через mcs.
// В Unity-проект (unity/Assets) этот файл не входит и в сборку не попадает.
// Объявлены лишь те члены API, которые реально использует игровой код.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return 0f; } }
        public float sqrMagnitude { get { return 0f; } }
        public Vector2 normalized { get { return this; } }
        public static Vector2 zero { get { return new Vector2(); } }
        public static Vector2 one { get { return new Vector2(); } }
        public static Vector2 up { get { return new Vector2(); } }
        public static float Distance(Vector2 a, Vector2 b) { return 0f; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return a; }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return a; }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return a; }
        public static Vector2 operator *(Vector2 a, float b) { return a; }
        public static Vector2 operator /(Vector2 a, float b) { return a; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }
        public float magnitude { get { return 0f; } }
        public float sqrMagnitude { get { return 0f; } }
        public Vector3 normalized { get { return this; } }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 one { get { return new Vector3(); } }
        public static Vector3 up { get { return new Vector3(); } }
        public static Vector3 down { get { return new Vector3(); } }
        public static Vector3 left { get { return new Vector3(); } }
        public static Vector3 right { get { return new Vector3(); } }
        public static Vector3 forward { get { return new Vector3(); } }
        public static Vector3 back { get { return new Vector3(); } }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
        public static float Dot(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return a; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a; }
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) { return a; }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator -(Vector3 a) { return a; }
        public static Vector3 operator *(Vector3 a, float b) { return a; }
        public static Vector3 operator *(float b, Vector3 a) { return a; }
        public static Vector3 operator /(Vector3 a, float b) { return a; }
        public static bool operator ==(Vector3 a, Vector3 b) { return false; }
        public static bool operator !=(Vector3 a, Vector3 b) { return false; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity { get { return new Quaternion(); } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion Euler(Vector3 v) { return identity; }
        public static Quaternion AngleAxis(float a, Vector3 axis) { return identity; }
        public static Quaternion LookRotation(Vector3 f) { return identity; }
        public static Quaternion LookRotation(Vector3 f, Vector3 up) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
        public Vector3 eulerAngles { get { return Vector3.zero; } set { } }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
        public static Vector3 operator *(Quaternion a, Vector3 b) { return b; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(); } }
        public static Color black { get { return new Color(); } }
        public static Color clear { get { return new Color(); } }
        public static Color red { get { return new Color(); } }
        public static Color green { get { return new Color(); } }
        public static Color blue { get { return new Color(); } }
        public static Color yellow { get { return new Color(); } }
        public static Color gray { get { return new Color(); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
        public static Color HSVToRGB(float h, float s, float v) { return new Color(); }
        public static void RGBToHSV(Color c, out float h, out float s, out float v) { h = 0; s = 0; v = 0; }
        public static Color operator *(Color a, float b) { return a; }
        public static Color operator *(Color a, Color b) { return a; }
        public static Color operator +(Color a, Color b) { return a; }
        public static implicit operator Vector4(Color c) { return new Vector4(); }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) { return new Color32(); }
        public static implicit operator Color(Color32 c) { return new Color(); }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
    }

    public struct Bounds
    {
        public Vector3 center, size, extents, min, max;
        public Bounds(Vector3 c, Vector3 s) { center = c; size = s; extents = s; min = c; max = c; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Infinity = float.PositiveInfinity;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public static float Abs(float f) { return 0f; }
        public static int Abs(int f) { return 0; }
        public static float Sign(float f) { return 0f; }
        public static float Sin(float f) { return 0f; }
        public static float Cos(float f) { return 0f; }
        public static float Tan(float f) { return 0f; }
        public static float Atan2(float y, float x) { return 0f; }
        public static float Sqrt(float f) { return 0f; }
        public static float Pow(float f, float p) { return 0f; }
        public static float Floor(float f) { return 0f; }
        public static int FloorToInt(float f) { return 0; }
        public static int RoundToInt(float f) { return 0; }
        public static int CeilToInt(float f) { return 0; }
        public static float Min(float a, float b) { return 0f; }
        public static int Min(int a, int b) { return 0; }
        public static float Max(float a, float b) { return 0f; }
        public static int Max(int a, int b) { return 0; }
        public static float Clamp(float v, float a, float b) { return 0f; }
        public static int Clamp(int v, int a, int b) { return 0; }
        public static float Clamp01(float v) { return 0f; }
        public static float Lerp(float a, float b, float t) { return 0f; }
        public static float LerpAngle(float a, float b, float t) { return 0f; }
        public static float MoveTowards(float a, float b, float d) { return 0f; }
        public static float DeltaAngle(float a, float b) { return 0f; }
        public static float Repeat(float t, float len) { return 0f; }
        public static float PingPong(float t, float len) { return 0f; }
        public static float SmoothStep(float a, float b, float t) { return 0f; }
        public static float PerlinNoise(float x, float y) { return 0f; }
        public static bool Approximately(float a, float b) { return false; }
    }

    public static class Random
    {
        public static float value { get { return 0f; } }
        public static Vector3 insideUnitSphere { get { return Vector3.zero; } }
        public static Vector2 insideUnitCircle { get { return Vector2.zero; } }
        public static float Range(float a, float b) { return 0f; }
        public static int Range(int a, int b) { return 0; }
        public static void InitState(int seed) { }
    }

    public static class Time
    {
        public static float time { get { return 0f; } }
        public static float deltaTime { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0f; } }
        public static float fixedDeltaTime { get { return 0f; } }
        public static float timeScale { get { return 0f; } set { } }
    }

    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
    }

    public class Object
    {
        public string name { get; set; }
        public int GetInstanceID() { return 0; }
        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float t) { }
        public static void DestroyImmediate(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T o) where T : Object { return o; }
        public static bool operator ==(Object a, Object b) { return false; }
        public static bool operator !=(Object a, Object b) { return false; }
        public static implicit operator bool(Object o) { return false; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
    }

    public class Component : Object
    {
        public GameObject gameObject { get { return null; } }
        public Transform transform { get { return null; } }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public T GetComponentInParent<T>() where T : class { return null; }
        public T[] GetComponentsInChildren<T>() where T : class { return null; }
    }

    public class Behaviour : Component { public bool enabled { get; set; } }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 right { get; set; }
        public Vector3 up { get; set; }
        public Transform parent { get; set; }
        public int childCount { get { return 0; } }
        public void SetParent(Transform p) { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public Transform GetChild(int i) { return null; }
        public Transform Find(string n) { return null; }
        public void LookAt(Vector3 p) { }
        public void Translate(Vector3 v) { }
        public void Rotate(Vector3 v) { }
        public Vector3 TransformDirection(Vector3 v) { return v; }
        public Vector3 InverseTransformDirection(Vector3 v) { return v; }
        public Vector3 TransformPoint(Vector3 v) { return v; }
        public IEnumerator GetEnumerator() { return null; }
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public Transform transform { get { return null; } }
        public int layer { get; set; }
        public string tag { get; set; }
        public bool activeSelf { get { return false; } }
        public bool activeInHierarchy { get { return false; } }
        public void SetActive(bool v) { }
        public T AddComponent<T>() where T : Component { return null; }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public static GameObject CreatePrimitive(PrimitiveType t) { return null; }
        public static GameObject Find(string n) { return null; }
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator r) { return null; }
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
        public void Invoke(string method, float t) { }
        public void CancelInvoke() { }
        public void CancelInvoke(string method) { }
        public bool IsInvoking(string method) { return false; }
    }

    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class WaitForEndOfFrame : YieldInstruction { }

    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public int[] triangles { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public Vector2[] uv2 { get; set; }
        public Color[] colors { get; set; }
        public Bounds bounds { get; set; }
        public int vertexCount { get { return 0; } }
        public void Clear() { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
        public void MarkDynamic() { }
        public IndexFormat indexFormat { get; set; }
    }

    public enum IndexFormat { UInt16, UInt32 }

    public class Texture : Object
    {
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
        public int width { get { return 0; } }
        public int height { get { return 0; } }
    }

    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureFormat { RGBA32, RGB24, ARGB32 }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { }
        public Texture2D(int w, int h, TextureFormat f, bool mipChain) { }
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] c) { }
        public Color GetPixel(int x, int y) { return Color.white; }
        public void Apply() { }
        public static Texture2D whiteTexture { get { return null; } }
    }

    public class Sprite : Object
    {
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot) { return null; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu) { return null; }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) { return null; }
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public Material(Material m) { }
        public Shader shader { get; set; }
        public Color color { get; set; }
        public Texture mainTexture { get; set; }
        public int renderQueue { get; set; }
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float f) { }
        public void SetInt(string n, int i) { }
        public void SetTexture(string n, Texture t) { }
        public void SetVector(string n, Vector4 v) { }
        public void SetTextureScale(string n, Vector2 s) { }
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
        public bool HasProperty(string n) { return false; }
    }

    public class Renderer : Component
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public Material[] materials { get; set; }
        public Material[] sharedMaterials { get; set; }
        public bool enabled { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public Bounds bounds { get { return new Bounds(); } }
    }

    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh mesh { get; set; } public Mesh sharedMesh { get; set; } }

    public class Collider : Component
    {
        public bool isTrigger { get; set; }
        public bool enabled { get; set; }
        public Bounds bounds { get { return new Bounds(); } }
        public Material material { get; set; }
    }

    public class BoxCollider : Collider { public Vector3 size { get; set; } public Vector3 center { get; set; } }
    public class SphereCollider : Collider { public float radius { get; set; } public Vector3 center { get; set; } }
    public class CapsuleCollider : Collider
    {
        public float radius { get; set; }
        public float height { get; set; }
        public Vector3 center { get; set; }
        public int direction { get; set; }
    }
    public class MeshCollider : Collider { public Mesh sharedMesh { get; set; } public bool convex { get; set; } }

    public class CharacterController : Collider
    {
        public bool isGrounded { get { return false; } }
        public float height { get; set; }
        public float radius { get; set; }
        public Vector3 center { get; set; }
        public float slopeLimit { get; set; }
        public float stepOffset { get; set; }
        public float skinWidth { get; set; }
        public float minMoveDistance { get; set; }
        public Vector3 velocity { get { return Vector3.zero; } }
        public CollisionFlags Move(Vector3 motion) { return CollisionFlags.None; }
    }

    [Flags]
    public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public class Rigidbody : Component
    {
        public bool isKinematic { get; set; }
        public bool useGravity { get; set; }
        public Vector3 velocity { get; set; }
    }

    public struct RaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Collider collider;
        public Transform transform;
    }

    public static class Physics
    {
        public static Vector3 gravity { get; set; }
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit hit, float max) { hit = new RaycastHit(); return false; }
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit hit, float max, int layerMask) { hit = new RaycastHit(); return false; }
        public static bool SphereCast(Vector3 o, float r, Vector3 d, out RaycastHit hit, float max, int layerMask) { hit = new RaycastHit(); return false; }
        public static Collider[] OverlapSphere(Vector3 p, float r) { return null; }
        public static Collider[] OverlapSphere(Vector3 p, float r, int layerMask) { return null; }
        public static void IgnoreLayerCollision(int a, int b, bool ignore) { }
    }

    public struct LayerMask
    {
        public static int GetMask(params string[] names) { return 0; }
        public static int NameToLayer(string n) { return 0; }
    }

    public class Camera : Behaviour
    {
        public static Camera main { get { return null; } }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public bool allowHDR { get; set; }
        public int cullingMask { get; set; }
        public int depth { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 p) { return Vector3.zero; }
        public Vector3 ScreenToWorldPoint(Vector3 p) { return Vector3.zero; }
    }

    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public float spotAngle { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; }
        public float shadowBias { get; set; }
        public float shadowNormalBias { get; set; }
    }

    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }

    public static class RenderSettings
    {
        public static bool fog { get; set; }
        public static Color fogColor { get; set; }
        public static FogMode fogMode { get; set; }
        public static float fogDensity { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
        public static Color ambientLight { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static float ambientIntensity { get; set; }
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Material skybox { get; set; }
    }

    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }

    public static class QualitySettings
    {
        public static int antiAliasing { get; set; }
        public static int vSyncCount { get; set; }
        public static float shadowDistance { get; set; }
        public static ShadowResolution shadowResolution { get; set; }
        public static int pixelLightCount { get; set; }
    }

    public enum ShadowResolution { Low, Medium, High, VeryHigh }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static string dataPath { get { return ""; } }
        public static string persistentDataPath { get { return ""; } }
        public static RuntimePlatform platform { get { return RuntimePlatform.Android; } }
        public static bool isMobilePlatform { get { return false; } }
        public static bool isEditor { get { return false; } }
        public static void Quit() { }
    }

    public enum RuntimePlatform { WindowsEditor, WindowsPlayer, LinuxEditor, LinuxPlayer, OSXEditor, OSXPlayer, Android, IPhonePlayer }

    public static class Screen
    {
        public static int width { get { return 0; } }
        public static int height { get { return 0; } }
        public static int sleepTimeout { get; set; }
        public static ScreenOrientation orientation { get; set; }
    }

    public enum ScreenOrientation { Portrait, LandscapeLeft, LandscapeRight, AutoRotation }
    public static class SleepTimeout { public const int NeverSleep = -1; }

    public static class Input
    {
        public static float GetAxis(string n) { return 0f; }
        public static float GetAxisRaw(string n) { return 0f; }
        public static bool GetKey(KeyCode k) { return false; }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetKeyUp(KeyCode k) { return false; }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static int touchCount { get { return 0; } }
        public static Touch GetTouch(int i) { return new Touch(); }
        public static bool touchSupported { get { return false; } }
        public static bool multiTouchEnabled { get; set; }
    }

    public struct Touch
    {
        public int fingerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 deltaPosition { get; set; }
        public TouchPhase phase { get; set; }
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public enum KeyCode
    {
        None, Space, Escape, Return, W, A, S, D, E, Q, R, LeftShift,
        UpArrow, DownArrow, LeftArrow, RightArrow, Mouse0, Mouse1
    }

    public class AudioClip : Object
    {
        public int samples { get { return 0; } }
        public int channels { get { return 0; } }
        public float length { get { return 0f; } }
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) { return null; }
        public bool SetData(float[] data, int offsetSamples) { return false; }
        public bool GetData(float[] data, int offsetSamples) { return false; }
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public bool isPlaying { get { return false; } }
        public float spatialBlend { get; set; }
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip c) { }
        public void PlayOneShot(AudioClip c, float v) { }
    }

    public class AudioListener : Behaviour
    {
        public static float volume { get; set; }
        public static bool pause { get; set; }
    }

    public class Font : Object
    {
        public Material material { get; set; }
        public static Font CreateDynamicFontFromOSFont(string name, int size) { return null; }
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
        public static T Load<T>(string path) where T : Object { return null; }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect { get { return new Rect(); } }
    }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public Camera worldCamera { get; set; }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Gradient { }
    public class AnimationCurve { }

    namespace Rendering
    {
        public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
        public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    }

    namespace UI
    {
        public class Graphic : Behaviour
        {
            public Color color { get; set; }
            public bool raycastTarget { get; set; }
            public RectTransform rectTransform { get { return null; } }
        }

        public class Image : Graphic
        {
            public Sprite sprite { get; set; }
            public Image.Type type { get; set; }
            public bool preserveAspect { get; set; }
            public enum Type { Simple, Sliced, Tiled, Filled }
        }

        public class Text : Graphic
        {
            public string text { get; set; }
            public Font font { get; set; }
            public int fontSize { get; set; }
            public FontStyle fontStyle { get; set; }
            public TextAnchor alignment { get; set; }
            public bool resizeTextForBestFit { get; set; }
            public HorizontalWrapMode horizontalOverflow { get; set; }
            public VerticalWrapMode verticalOverflow { get; set; }
            public float lineSpacing { get; set; }
            public bool supportRichText { get; set; }
        }

        public enum HorizontalWrapMode { Wrap, Overflow }
        public enum VerticalWrapMode { Truncate, Overflow }

        public class Selectable : Behaviour
        {
            public bool interactable { get; set; }
            public ColorBlock colors { get; set; }
            public Graphic targetGraphic { get; set; }
            public Image image { get; set; }
        }

        public struct ColorBlock
        {
            public Color normalColor;
            public Color highlightedColor;
            public Color pressedColor;
            public Color selectedColor;
            public Color disabledColor;
            public float colorMultiplier;
            public float fadeDuration;
            public static ColorBlock defaultColorBlock { get { return new ColorBlock(); } }
        }

        public class Button : Selectable
        {
            public ButtonClickedEvent onClick { get { return null; } }
            public class ButtonClickedEvent { public void AddListener(Action a) { } public void RemoveAllListeners() { } }
        }

        public class InputField : Selectable
        {
            public string text { get; set; }
            public int characterLimit { get; set; }
            public Text textComponent { get; set; }
            public Graphic placeholder { get; set; }
            public ContentType contentType { get; set; }
            public enum ContentType { Standard, Alphanumeric, IntegerNumber, DecimalNumber }
            public OnChangeEvent onValueChanged { get { return null; } }
            public class OnChangeEvent { public void AddListener(Action<string> a) { } }
        }

        public class CanvasScaler : Behaviour
        {
            public ScaleMode uiScaleMode { get; set; }
            public Vector2 referenceResolution { get; set; }
            public float matchWidthOrHeight { get; set; }
            public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        }

        public class GraphicRaycaster : Behaviour { }
    }

    namespace EventSystems
    {
        public class EventSystem : Behaviour
        {
            public static EventSystem current { get { return null; } }
        }
        public class StandaloneInputModule : Behaviour { }
    }
}

namespace UnityEditor
{
    using UnityEngine;

    public static class EditorApplication { public static void Exit(int code) { } }

    public class BuildPlayerOptions
    {
        public string[] scenes;
        public string locationPathName;
        public BuildTarget target;
        public BuildTargetGroup targetGroup;
        public BuildOptions options;
    }

    public enum BuildTarget { Android, StandaloneLinux64, StandaloneWindows64 }
    public enum BuildTargetGroup { Android, Standalone }
    [Flags] public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4 }

    public static class BuildPipeline
    {
        public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) { return null; }
    }

    namespace Build
    {
        namespace Reporting
        {
            public class BuildSummary { public BuildResult result; public ulong totalSize; }
            public class BuildReport : Object { public BuildSummary summary { get { return null; } } }
            public enum BuildResult { Unknown, Succeeded, Failed, Cancelled }
        }
    }

    public static class PlayerSettings
    {
        public static string productName { get; set; }
        public static string companyName { get; set; }
        public static string bundleVersion { get; set; }
        public static ColorSpace colorSpace { get; set; }
        public static bool use32BitDisplayBuffer { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static void SetApplicationIdentifier(BuildTargetGroup g, string id) { }
        public static void SetScriptingBackend(BuildTargetGroup g, ScriptingImplementation b) { }
        public static void SetManagedStrippingLevel(BuildTargetGroup g, ManagedStrippingLevel l) { }
        public static void SetGraphicsAPIs(BuildTarget t, UnityEngine.Rendering.GraphicsDeviceType[] apis) { }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget t, bool v) { }

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static bool forceInternetPermission { get; set; }
            public static int bundleVersionCode { get; set; }
            public static bool useCustomKeystore { get; set; }
        }
    }

    public enum ColorSpace { Gamma, Linear }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }

    public class SerializedProperty
    {
        public int arraySize { get; set; }
        public Object objectReferenceValue { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int i) { return null; }
        public void InsertArrayElementAtIndex(int i) { }
    }

    public class SerializedObject
    {
        public SerializedObject(Object o) { }
        public SerializedProperty FindProperty(string path) { return null; }
        public bool ApplyModifiedProperties() { return false; }
        public void Update() { }
    }
    public enum ScriptingImplementation { Mono2x, IL2CPP }
    public enum ManagedStrippingLevel { Disabled, Low, Medium, High }
    public enum AndroidSdkVersions { AndroidApiLevel22 = 22, AndroidApiLevel23 = 23, AndroidApiLevel24 = 24, AndroidApiLevel29 = 29, AndroidApiLevel33 = 33, AndroidApiLevel34 = 34 }
    [Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, All = 3 }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static Object[] LoadAllAssetsAtPath(string p) { return null; }
        public static void SaveAssets() { }
        public static bool IsValidFolder(string p) { return false; }
        public static string CreateFolder(string parent, string name) { return ""; }
    }

    namespace SceneManagement
    {
        using UnityEngine.SceneManagement;
        public static class EditorSceneManager
        {
            public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new Scene(); }
            public static bool SaveScene(Scene s, string path) { return false; }
            public static Scene OpenScene(string path) { return new Scene(); }
        }
        public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
        public enum NewSceneMode { Single, Additive }
    }
}

namespace UnityEngine
{
    namespace SceneManagement
    {
        public struct Scene { public string path { get { return ""; } } public bool IsValid() { return false; } }
        public static class SceneManager
        {
            public static Scene GetActiveScene() { return new Scene(); }
            public static void LoadScene(string name) { }
        }
    }

    namespace Rendering
    {
        public enum GraphicsDeviceType { OpenGLES2 = 8, OpenGLES3 = 11, Vulkan = 21 }
    }
}
