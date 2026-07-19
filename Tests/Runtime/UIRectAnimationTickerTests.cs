using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UIRect.Tests
{
    public class UIRectAnimationTickerTests
    {
        private GameObject _go;
        private GameObject _canvasGo;
        private UIRectImage _image;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestUIRectImage");
            _go.AddComponent<RectTransform>();
            _go.AddComponent<CanvasRenderer>();
            _image = _go.AddComponent<UIRectImage>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            if (_canvasGo != null)
                Object.DestroyImmediate(_canvasGo);
            // Drain leftover entries so ticker state can't leak between tests.
            UIRectAnimationTicker.Tick();
        }

        [Test]
        public void AnimateTo_RegistersWithTicker()
        {
            _image.AnimateTo(new UIRectStyle { FillColor = Color.red }, duration: 1f);

            Assert.AreEqual(1, UIRectAnimationTicker.ActiveCount);
        }

        [Test]
        public void Tick_ZeroDuration_SnapsCompletesAndUnregisters()
        {
            bool completed = false;
            _image.AnimateTo(new UIRectStyle { FillColor = Color.red }, duration: 0f,
                onComplete: () => completed = true);

            UIRectAnimationTicker.Tick();

            Assert.AreEqual(Color.red, _image.fillColor, "A zero-duration animation must snap to the target on the first tick.");
            Assert.IsTrue(completed, "onComplete must fire on the completing tick.");
            Assert.AreEqual(0, UIRectAnimationTicker.ActiveCount, "A completed animation must unregister.");
        }

        [Test]
        public void Tick_ChainedAnimateToInOnComplete_KeepsEntryAlive()
        {
            _image.AnimateTo(new UIRectStyle { FillColor = Color.red }, duration: 0f,
                onComplete: () => _image.AnimateTo(new UIRectStyle { FillColor = Color.blue }, duration: 0f));

            UIRectAnimationTicker.Tick();
            Assert.AreEqual(1, UIRectAnimationTicker.ActiveCount,
                "An onComplete that chains a new AnimateTo must keep the entry registered.");

            UIRectAnimationTicker.Tick();
            Assert.AreEqual(Color.blue, _image.fillColor, "The chained animation must run on the next tick.");
            Assert.AreEqual(0, UIRectAnimationTicker.ActiveCount);
        }

        [Test]
        public void Tick_DestroyedHost_IsPrunedWithoutThrowing()
        {
            _image.AnimateTo(new UIRectStyle { FillColor = Color.red }, duration: 1000f);
            Object.DestroyImmediate(_go);

            Assert.DoesNotThrow(UIRectAnimationTicker.Tick);
            Assert.AreEqual(0, UIRectAnimationTicker.ActiveCount, "Destroyed hosts must be pruned.");
        }

        // End-to-end: the canvas render loop must drive the animation without any per-component Update().
        [UnityTest]
        public IEnumerator Animation_AdvancesViaCanvasRenderLoop()
        {
            _canvasGo = new GameObject("Canvas", typeof(Canvas));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _go.transform.SetParent(_canvasGo.transform, false);

            // Stock material: headless CI can't resolve the UIRect shader, which isn't under test.
            _image.material = Canvas.GetDefaultCanvasMaterial();

            bool completed = false;
            _image.AnimateTo(new UIRectStyle { FillColor = Color.red }, duration: 0.05f,
                onComplete: () => completed = true);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(completed, "The ticker must complete the animation from the canvas render loop.");
            Assert.AreEqual(Color.red, _image.fillColor);
            Assert.AreEqual(0, UIRectAnimationTicker.ActiveCount);
        }
    }
}
