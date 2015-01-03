﻿using System;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.ComponentModel;
using System.Diagnostics;
using Fasterflect;
using System.Windows.Input;

namespace GitViz.Logic.Mvvm
{
    /// <summary>
    /// This is an implementation of a command that can be invoked through convention
    /// </summary>
    public class ConventionalCommand : ICommand
    {
        private FrameworkElement boundObject;

        private String path;

        private Boolean isAsync;

        private String waitMessage;

        public ConventionalCommand(FrameworkElement boundObject, String path, Boolean isAsync, String waitMessage)
        {
            this.boundObject = boundObject;
            this.path = path;
            this.isAsync = isAsync;
            this.waitMessage = waitMessage;
            this.boundObject.DataContextChanged += boundObject_DataContextChanged;
            RelatedViewModel = boundObject.DataContext as INotifyPropertyChanged;
        }

        void boundObject_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RelatedViewModel = e.NewValue as INotifyPropertyChanged;
        }

        INotifyPropertyChanged relatedViewModel;
        MethodInfo canExecute;
        MethodInfo execute;

        private INotifyPropertyChanged RelatedViewModel
        {

            get
            {
                return relatedViewModel;
            }
            set
            {
                if (relatedViewModel != null)
                {
                    relatedViewModel.PropertyChanged -= relatedViewModel_PropertyChanged;
                }
                relatedViewModel = value;
                if (relatedViewModel != null)
                {
                    Type vmt = relatedViewModel.GetType();
                    canExecute = vmt.GetMethod("CanExecute" + path, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    execute = vmt.GetMethod("Execute" + path, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (execute == null)
                    {
                        throw new ArgumentException("Binding Error - Command Execute" + path + " not found in viewmodel of type " + vmt.GetType());
                        //Debug.WriteLine("Binding Error - Command Execute" + path + " not found in viewmodel of type " + vmt.GetType());
                    }
                    relatedViewModel.PropertyChanged += relatedViewModel_PropertyChanged;
                }
                OnCanExecuteChanged();
            }
        }

        void relatedViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CanExecute" + path)
            {
                OnCanExecuteChanged();
            }
        }

        public event EventHandler CanExecuteChanged;

        protected virtual void OnCanExecuteChanged()
        {
            EventHandler temp = CanExecuteChanged;
            if (temp != null)
            {
                temp(this, EventArgs.Empty);
            }
        }

        public bool CanExecute(object parameter)
        {
            if (RelatedViewModel == null) return false;
            if (canExecute == null) return true;

            return (Boolean)canExecute.Call(RelatedViewModel, new Object[] { parameter });
        }

        public void Execute(object parameter)
        {
            if (RelatedViewModel == null) return;
            ExecuteMethodCall(parameter);
        }

        private void ExecuteMethodCall(object parameter)
        {
            try
            {
                execute.Call(RelatedViewModel, new Object[] { parameter });
            }
            catch (Exception ex)
            {
                //TODO: Log error.
            }

        }


    }

    public class DesignerConventionalCommand : ICommand
    {
        bool ICommand.CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;

        protected void OnCanExecuteChanged()
        {
            CanExecuteChanged(this, EventArgs.Empty);
        }

        void ICommand.Execute(object parameter)
        {

        }
    }

    public class MvvmCommand : MarkupExtension
    {

        /// <summary>
        /// This is the path of the command, the ViewModel should have a method called ExecutePath to 
        /// make everything work.
        /// </summary>
        public String Path { get; set; }

        [DefaultValue(false)]
        public Boolean IsAsync { get; set; }

        [DefaultValue("")]
        public String WaitMessage { get; set; }

        public MvvmCommand()
        {
            WaitMessage = String.Empty;
            Path = String.Empty;
        }

        public MvvmCommand(String path)
            : this()
        {
            this.Path = path;
        }

        /// <summary>
        /// we need to provide the ICommand that will take care of command invocation.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (WpfExtensions.IsInDesignModeStatic) return new DesignerConventionalCommand();

            var service = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            if (service == null) return false;


            if (service.TargetObject.GetType().FullName == "System.Windows.SharedDp")
                return this;

            FrameworkElement dobj = service.TargetObject as FrameworkElement;
            if (dobj == null) throw new ApplicationException("Cannot do Conventional Command Binding");
            return new ConventionalCommand(dobj, Path, IsAsync, WaitMessage);
        }
    }
}
