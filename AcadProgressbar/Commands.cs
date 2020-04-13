using System;
using Autodesk.Windows;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using acApp = Autodesk.AutoCAD.ApplicationServices.Application;

using System.Threading;
using System.Globalization;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

[assembly: CommandClass(typeof(AcadProgressbar.Commands))]
[assembly: ExtensionApplication(typeof(AcadProgressbar.Module))]

namespace AcadProgressbar

{
        // to make the AutoCAD managed runtime happy
    public class Module : IExtensionApplication
    {
        public void Initialize()
        {
        }

        public void Terminate()
        {
        }
    }

    public class Commands
    {
        public class MyVarOverride : IDisposable
        {
            object oldValue;
            string varName;

            public MyVarOverride(string name, object value)
            {
                varName = name;
                oldValue = acApp.GetSystemVariable(name);
                acApp.SetSystemVariable(name, value);
            }

            public void Dispose()
            {
                acApp.SetSystemVariable(varName, oldValue);
            }
        }

        public class MyData
        {
            public int counter = 0;
            public bool delay = true;

            // since the callback data can be reused, be sure
            // to reset it before invoking the task dialog
            public void Reset()
            {
                counter = 0;
                delay = true;
            }
        }

        #region HELPER METHODS
        // helper method for processing the callback both in the 
        // data-member case and the callback argument case
        private bool handleCallback(ActiveTaskDialog taskDialog,
                                    TaskDialogCallbackArgs args,
                                    MyData callbackData)
        {
            // This gets called continuously until we finished completely
            if (args.Notification == TaskDialogNotification.Timer)
            {
                // To make it longer we do some delay in every second call
                if (callbackData.delay)
                {
                    System.Threading.Thread.Sleep(1000);
                }
                else
                {
                    callbackData.counter += 10;
                    taskDialog.SetProgressBarRange(0, 100);
                    taskDialog.SetProgressBarPosition(
                        callbackData.counter);

                    // This is the main action - adding 100 lines 1 by 1
                    Database db = HostApplicationServices.WorkingDatabase;
                    Transaction tr = db.TransactionManager.TopTransaction;
                    BlockTable bt = (BlockTable)tr.GetObject(
                      db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                      bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    Line ln = new Line(
                      new Point3d(0, callbackData.counter, 0),
                      new Point3d(10, callbackData.counter, 0));
                    ms.AppendEntity(ln);
                    tr.AddNewlyCreatedDBObject(ln, true);

                    // To make it appear on the screen - might be a bit costly
                    tr.TransactionManager.QueueForGraphicsFlush();
                    acApp.DocumentManager.MdiActiveDocument.Editor.Regen();

                    // We are finished
                    if (callbackData.counter >= 100)
                    {
                        // We only have a cancel button, 
                        // so this is what we can press

                        taskDialog.ClickButton(
                          (int)WinForms.DialogResult.Cancel);
                        return true;
                    }
                }
                callbackData.delay = !callbackData.delay;
            }
            else if (
              args.Notification == TaskDialogNotification.ButtonClicked)
            {
                // we only have a cancel button
                if (args.ButtonId == (int)WinForms.DialogResult.Cancel)
                {
                    return false;
                }
            }
            return true;
        }

        private TaskDialog CreateTaskDialog()
        {
            TaskDialog td = new TaskDialog();
            td.WindowTitle = "Adding lines";
            td.ContentText = "This operation adds 10 lines one at a " +
                            "time and might take a bit of time.";
            td.EnableHyperlinks = true;
            td.ExpandedText = "This operation might be lengthy.";
            td.ExpandFooterArea = true;
            td.AllowDialogCancellation = true;
            td.ShowProgressBar = true;
            td.CallbackTimer = true;
            td.CommonButtons = TaskDialogCommonButtons.Cancel;
            return td;
        }
        #endregion

        #region TASK DIALOG USING CALLBACK DATA ARGUMENT

        /////////////////////////////////////////////////////////////////
        // This sample uses a local instance of the callback data. 
        // Since the TaskDialog class needs to convert the callback data
        // to an IntPtr to pass it across the managed-unmanaged divide,
        // be sure to convert it to an IntPtr before passing it off
        // to the TaskDialog instance. 
        //
        // This case requires more code than the member-based sample 
        // below, but is useful when a callback is shared 
        // between multiple task dialogs.
        /////////////////////////////////////////////////////////////////
        // task dialog callback that uses the mpCallbackData argument

        public bool TaskDialogCallback(ActiveTaskDialog taskDialog,
                                        TaskDialogCallbackArgs args,
                                        object mpCallbackData)
        {
            // convert the callback data from an IntPtr to the actual
            // object using GCHandle
            GCHandle callbackDataHandle =
              GCHandle.FromIntPtr((IntPtr)mpCallbackData);
            MyData callbackData = (MyData)callbackDataHandle.Target;

            // use the helper method to do the actual processing
            return handleCallback(taskDialog, args, callbackData);
        }

        [CommandMethod("ShowTaskDialog")]
        public void ShowTaskDialog()
        {
            Database db = HostApplicationServices.WorkingDatabase;
            using (
              Transaction tr = db.TransactionManager.StartTransaction())
            {
                // create the task dialog and initialize the callback method
                TaskDialog td = CreateTaskDialog();
                td.Callback = new TaskDialogCallback(TaskDialogCallback);

                // create the callback data and convert it to an IntPtr
                // using GCHandle
                MyData cbData = new MyData();
                GCHandle cbDataHandle = GCHandle.Alloc(cbData);
                td.CallbackData = GCHandle.ToIntPtr(cbDataHandle);

                // Just to minimize the "Regenerating model" messages
                using (new MyVarOverride("NOMUTT", 1))
                {
                    td.Show(Application.MainWindow.Handle);
                }

                // If the dialog was not cancelled before it finished
                // adding the lines then commit transaction
                if (memberCallbackData.counter >= 100)
                    tr.Commit();

                // be sure to clean up the gc handle before returning
                cbDataHandle.Free();
            }
        }
        #endregion

        #region TASK DIALOG USING DATA-MEMBER-BASED CALLBACK DATA

        /////////////////////////////////////////////////////////////////
        // This sample uses a data member for the callback data. 
        // This avoids having to pass the callback data as an IntPtr.
        /////////////////////////////////////////////////////////////////

        // member-based callback data - 
        // used with MemberTaskDialogCallback

        MyData memberCallbackData = new MyData();

        // task dialog callback that uses the callback data member; 
        // does not use mpCallbackData

        public bool TaskDialogCallbackUsingMemberData(
          ActiveTaskDialog taskDialog,
          TaskDialogCallbackArgs args,
          object mpCallbackData)
        {
            // use the helper method to do the actual processing
            return handleCallback(taskDialog, args, memberCallbackData);
        }

        [CommandMethod("ShowTaskDialogWithDataMember")]
        public void ShowTaskDialogWithDataMember()
        {
            Database db = HostApplicationServices.WorkingDatabase;
            using (
              Transaction tr = db.TransactionManager.StartTransaction())
            {
                // create the task dialog and initialize the callback method
                TaskDialog td = CreateTaskDialog();
                td.Callback =
                  new TaskDialogCallback(TaskDialogCallbackUsingMemberData);

                // make sure the callback data is initialized before 
                // invoking the task dialog
                memberCallbackData.Reset();

                // Just to minimize the "Regenerating model" messages
                using (new MyVarOverride("NOMUTT", 1))
                {
                    td.Show(Application.MainWindow.Handle);
                }

                // If the dialog was not cancelled before it finished
                // adding the lines then commit transaction
                if (memberCallbackData.counter >= 100)
                    tr.Commit();
            }
        }
        #endregion
    }
}