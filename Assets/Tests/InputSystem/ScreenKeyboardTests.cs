using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

/// <summary>
/// Behavioral tests for Screen Keyboard
/// These tests ensure that screen keyboard is behaving in the same manner on all platforms
/// Most likely, some OS' might have a different behavior, in that case if possible, the backend must simulate the behavior described in these tests
/// </summary>
public class ScreenKeyboardTests : InputTestFixture
{
    static ScreenKeyboard s_TargetKeyboard;
    const int kFrameTimeout = 30;

    // TODO: call count
    public class CallbackInfo<T>
    {
        public T Data { private set; get; }
        public int Frame { private set; get; }
        public int ThreadId { private set; get; }
        public int CalledCount { private set; get; }

        public CallbackInfo()
        {
            Frame = -1;
            ThreadId = -1;
            CalledCount = 0;
        }

        public CallbackInfo(T initialData)
        {
            Data = initialData;
            Frame = -1;
            ThreadId = -1;
            CalledCount = 0;
        }

        public void CallbackInvoked(T data)
        {
            Data = data;
            Frame = Time.frameCount;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            CalledCount++;
        }
    }

    // Workaround RangeInt not having ToString function
    private struct MyRangeInt
    {
        public int start;
        public int length;

        public static implicit operator MyRangeInt(RangeInt range)
        {
            return new MyRangeInt(range.start, range.length);
        }

        public MyRangeInt(int start, int length)
        {
            this.start = start;
            this.length = length;
        }

        public override string ToString()
        {
            return $"{start}, {length}";
        }
    }

    ScreenKeyboard keyboard
    {
        get
        {
            if (s_TargetKeyboard == null)
            {
#if UNITY_EDITOR
                s_TargetKeyboard = runtime.screenKeyboard;
                Assert.AreEqual(s_TargetKeyboard.GetType(), typeof(FakeScreenKeyboard));
#else
                s_TargetKeyboard = NativeInputRuntime.instance.screenKeyboard;
#if UNITY_ANDROID
                Assert.AreEqual(s_TargetKeyboard.GetType(), typeof(UnityEngine.InputSystem.Android.AndroidScreenKeyboard));
#elif UNITY_IOS
                Assert.AreEqual(s_TargetKeyboard.GetType(), typeof(UnityEngine.InputSystem.iOS.iOSScreenKeyboard));
#endif
#endif
                if (s_TargetKeyboard == null)
                    throw new Exception("No Screen Keyboard to test?");
                Console.WriteLine($"Testable Keyboards is: {s_TargetKeyboard.GetType().FullName}");
            }
            return s_TargetKeyboard;
        }
    }

    private IEnumerator ResetKeyboard()
    {
        // If there's a failure in test, the callbacks might not be properly cleaned up
        // So it's easier to clean them up before starting test
        keyboard.ClearListeners();
        return HideKeyboard();
    }

    private IEnumerator HideKeyboard()
    {
        if (keyboard.status != ScreenKeyboardStatus.Done)
        {
            keyboard.Hide();
            for (int i = 0; i < kFrameTimeout && keyboard.status != ScreenKeyboardStatus.Done; i++)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(ScreenKeyboardStatus.Done, keyboard.status, "Couldn't hide keyboard");
        }
    }

    private IEnumerator ShowKeyboard()
    {
        return ShowKeyboard(new ScreenKeyboardShowParams());
    }

    private IEnumerator ShowKeyboard(ScreenKeyboardShowParams showParams)
    {
        Assert.IsTrue(keyboard.status != ScreenKeyboardStatus.Visible, "Expected keybard to be not visible");

        keyboard.Show(showParams);
        for (int i = 0; i < kFrameTimeout && keyboard.status != ScreenKeyboardStatus.Visible; i++)
            yield return new WaitForFixedUpdate();
        Assert.AreEqual(ScreenKeyboardStatus.Visible, keyboard.status, "Couldn't show keyboard");
    }

    // TODO See that callbacks are not called when keyboard is not shown. ??? Do we really need this
    [UnityTest]
    public IEnumerator CheckShowHideOperations()
    {
        yield return ResetKeyboard();
        yield return ShowKeyboard();
        yield return HideKeyboard();
    }

    [UnityTest]
    public IEnumerator CheckStateCallback()
    {
        yield return ResetKeyboard();

        var stateCallbackInfo = new CallbackInfo<ScreenKeyboardStatus>(ScreenKeyboardStatus.Canceled);
        var stateCallback = new Action<ScreenKeyboardStatus>(
            (state) =>
            {
                stateCallbackInfo.CallbackInvoked(state);
            });
        keyboard.stateChanged += stateCallback;

        yield return ShowKeyboard();

        Assert.AreEqual(ScreenKeyboardStatus.Visible, stateCallbackInfo.Data);
        Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, stateCallbackInfo.ThreadId);
        Assert.AreEqual(1, stateCallbackInfo.CalledCount);
        // Don't check frame, since when you call Show the keyboard can appear only in next frame

        yield return HideKeyboard();

        Assert.AreEqual(ScreenKeyboardStatus.Done, stateCallbackInfo.Data);
        Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, stateCallbackInfo.ThreadId);
        Assert.AreEqual(2, stateCallbackInfo.CalledCount);
    }

    [UnityTest]
    public IEnumerator CheckInputFieldTextCallback([Values(true, false)] bool multiline)
    {
        yield return ResetKeyboard();

        var inputFieldTextCallbackInfo = new CallbackInfo<string>(string.Empty);
        var inputFieldCallback = new Action<string>(
            (text) =>
            {
                inputFieldTextCallbackInfo.CallbackInvoked(text);
            });
        keyboard.inputFieldTextChanged += inputFieldCallback;
        yield return ShowKeyboard(new ScreenKeyboardShowParams(){multiline = multiline });

        Assert.AreEqual(string.Empty, keyboard.inputFieldText);

        var targetText = "Hello";
        keyboard.inputFieldText = targetText;

        Assert.AreEqual(targetText, inputFieldTextCallbackInfo.Data);
        Assert.AreEqual(Time.frameCount, inputFieldTextCallbackInfo.Frame);
        Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, inputFieldTextCallbackInfo.ThreadId);
        Assert.AreEqual(1, inputFieldTextCallbackInfo.CalledCount);

        yield return HideKeyboard();
    }

    [UnityTest]
    public IEnumerator ChangeTextInsideInputFieldCallback([Values(true, false)] bool multiline)
    {
        yield return ResetKeyboard();

        var selectionCallbackInfo = new CallbackInfo<MyRangeInt>(new MyRangeInt(0, 0));
        var selectionCallback = new Action<RangeInt>((range) => { selectionCallbackInfo.CallbackInvoked(range); });

        keyboard.selectionChanged += selectionCallback;
        var inputFieldTextCallbackInfo = new CallbackInfo<string>(string.Empty);
        var inputFieldCallback = new Action<string>(
            (text) =>
            {
                inputFieldTextCallbackInfo.CallbackInvoked(text);
                if (text.Equals("12345"))
                {
                    // Change to text with same length
                    keyboard.inputFieldText = "11111";
                }
                else if (text.Equals("11111"))
                {
                    // Change to text with different length
                    keyboard.inputFieldText = "123456";
                }
                else
                {
                    // Change to same text, this shouldn't trigger a callback, since text didn't change
                    keyboard.inputFieldText = text;
                }
                
            });
        keyboard.inputFieldTextChanged += inputFieldCallback;

        yield return ShowKeyboard(new ScreenKeyboardShowParams() { multiline = multiline });
        
        var targetText = "12345";
        keyboard.inputFieldText = targetText;
        targetText = "123456";

        Assert.AreEqual(targetText, inputFieldTextCallbackInfo.Data);
        Assert.AreEqual(targetText, keyboard.inputFieldText);
        Assert.AreEqual(3, inputFieldTextCallbackInfo.CalledCount);
        Assert.AreEqual(2, selectionCallbackInfo.CalledCount);

        yield return HideKeyboard();
    }

    [UnityTest]
    public IEnumerator ChangeSelectionInsideSelectionCallback()
    {
        yield return ResetKeyboard();

        var selectionCallbackInfo = new CallbackInfo<MyRangeInt>(new MyRangeInt(0, 0));
        var selectionCallback = new Action<RangeInt>((range) =>
            {
                selectionCallbackInfo.CallbackInvoked(range); 
                keyboard.selection = new RangeInt(1, 0);
            });

        keyboard.selectionChanged += selectionCallback;
        yield return ShowKeyboard();

        keyboard.inputFieldText = "Hello";
        Assert.AreEqual(2, selectionCallbackInfo.CalledCount);
        Assert.AreEqual(new MyRangeInt(1, 0), (MyRangeInt)selectionCallbackInfo.Data);
        yield return HideKeyboard();
    }

    [UnityTest]
    public IEnumerator CheckInputFieldText([Values(true, false)] bool multiline)
    {
        yield return ResetKeyboard();
        var initiaText = "Placeholder";
        var targetText = "Hello";
        yield return ShowKeyboard(new ScreenKeyboardShowParams {initialText = initiaText, multiline =  multiline});

        Assert.AreEqual(initiaText, keyboard.inputFieldText);
        keyboard.inputFieldText = targetText;
        Assert.AreEqual(targetText, keyboard.inputFieldText);

        yield return HideKeyboard();

        Assert.AreEqual(targetText, keyboard.inputFieldText);
    }

    [UnityTest]
    public IEnumerator CheckSelectionCallbacks()
    {
        yield return ResetKeyboard();
        var selectionCallbackInfo = new CallbackInfo<MyRangeInt>(new MyRangeInt(0, 0));
        var selectionCallback = new Action<RangeInt>((range) =>
        {
            selectionCallbackInfo.CallbackInvoked(range);
        });

        keyboard.selectionChanged += selectionCallback;

        yield return ShowKeyboard();

        Assert.AreEqual(new MyRangeInt(0, 0), (MyRangeInt)keyboard.selection);

        var targetText = "Hello";
        keyboard.inputFieldText = targetText;

        Assert.AreEqual(new MyRangeInt(targetText.Length, 0), selectionCallbackInfo.Data);
        Assert.AreEqual(Time.frameCount, selectionCallbackInfo.Frame);
        Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, selectionCallbackInfo.ThreadId);
        Assert.AreEqual(1, selectionCallbackInfo.CalledCount);

        // Assign inputFieldTextChanged, and see that setting selection doesn't trigger it
        var inputFieldTextCallbackInfo = new CallbackInfo<string>(string.Empty);
        var inputFieldCallback = new Action<string>(
            (text) =>
            {
                inputFieldTextCallbackInfo.CallbackInvoked(text);;
            });
        keyboard.inputFieldTextChanged += inputFieldCallback;

        keyboard.selection = new RangeInt(1, 0);
        Assert.AreEqual(new MyRangeInt(1, 0), selectionCallbackInfo.Data);
        Assert.AreEqual(2, selectionCallbackInfo.CalledCount);

        // Calling selection shouldn't trigger inputFieldText callback
        Assert.AreEqual(0, inputFieldTextCallbackInfo.CalledCount);

        // TODO: check selection out of bounds behavior
        keyboard.selection = new RangeInt(targetText.Length, 0);

        yield return HideKeyboard();

        Assert.AreEqual(new MyRangeInt(targetText.Length, 0), (MyRangeInt)keyboard.selection);
    }
}
