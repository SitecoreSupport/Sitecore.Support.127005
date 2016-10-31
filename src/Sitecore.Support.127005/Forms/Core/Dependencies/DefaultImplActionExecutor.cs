// Generated by .NET Reflector from C:\work\Sitecores\sc81rev160519cm\Website\bin\Sitecore.Forms.Core.dll
namespace Sitecore.Support.Forms.Core.Dependencies
{
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Events;
  using Sitecore.Form.Core.Configuration;
  using Sitecore.Form.Core.ContentEditor.Data;
  using Sitecore.Form.Core.Data;
  using Sitecore.Form.Core.Pipelines.FormSubmit;
  using Sitecore.Form.Core.Utility;
  using Sitecore.Pipelines;
  using Sitecore.WFFM.Abstractions.Actions;
  using Sitecore.WFFM.Abstractions.Analytics;
  using Sitecore.WFFM.Abstractions.ContentEditor;
  using Sitecore.WFFM.Abstractions.Dependencies;
  using Sitecore.WFFM.Abstractions.Data;
  using Sitecore.WFFM.Abstractions.Shared;
  using System;
  using System.Collections.Generic;
  using System.Linq;

  [Serializable]
  public class DefaultImplActionExecutor : IActionExecutor
  {
    private readonly IAnalyticsTracker analyticsTracker;
    private readonly IWffmDataProvider dataProvider;
    private readonly IFieldProvider fieldProvider;
    private readonly IFormContext formContext;
    private readonly IItemRepository itemRepository;
    private readonly ILogger logger;
    private readonly IRequirementsChecker requirementsChecker;
    private readonly IResourceManager resourceManager;

    [UsedImplicitly]
    public DefaultImplActionExecutor(IItemRepository itemRepository, IRequirementsChecker requirementsChecker, ILogger logger, IResourceManager resourceManager, IAnalyticsTracker analyticsTracker, IWffmDataProvider dataProvider, IFieldProvider fieldProvider, IFormContext formContext)
    {
      Assert.ArgumentNotNull(itemRepository, nameof(itemRepository));
      Assert.ArgumentNotNull(requirementsChecker, nameof(requirementsChecker));
      Assert.ArgumentNotNull(logger, nameof(logger));
      Assert.ArgumentNotNull(resourceManager, nameof(resourceManager));
      Assert.ArgumentNotNull(analyticsTracker, nameof(analyticsTracker));
      Assert.ArgumentNotNull(dataProvider, nameof(dataProvider));
      Assert.ArgumentNotNull(fieldProvider, nameof(fieldProvider));
      Assert.ArgumentNotNull(formContext, nameof(formContext));
      this.itemRepository = itemRepository;
      this.requirementsChecker = requirementsChecker;
      this.logger = logger;
      this.resourceManager = resourceManager;
      this.analyticsTracker = analyticsTracker;
      this.dataProvider = dataProvider;
      this.fieldProvider = fieldProvider;
      this.formContext = formContext;
    }

    [Obsolete("Use another constructor")]
    public DefaultImplActionExecutor(IItemRepository itemRepository, IRequirementsChecker requirementsChecker, ILogger logger, IResourceManager resourceManager, IFactoryObjectsProvider factoryObjectsProvider, IAnalyticsTracker analyticsTracker, IWffmDataProvider dataProvider, IFieldProvider fieldProvider) : this(itemRepository, requirementsChecker, logger, resourceManager, analyticsTracker, dataProvider, fieldProvider, DependenciesManager.Resolve<IFormContext>())
    {
    }

    public virtual List<AdaptedControlResult> AdaptResult(IEnumerable<ControlResult> list, bool simpleAdapt) =>
        (from result in list select new AdaptedControlResult(result, this.fieldProvider, simpleAdapt)).ToList<AdaptedControlResult>();

    [Obsolete]
    public void Execute(ID formID, ControlResult[] list, IActionDefinition[] actions)
    {
      this.ExecuteSaving(formID, list, actions, false, null);
    }

    public void ExecuteChecking(ID formID, ControlResult[] fields, IActionDefinition[] actionDefinitions)
    {
      IActionDefinition definition = null;
      IFormItem item = this.itemRepository.CreateFormItem(formID);
      try
      {
        this.RaiseEvent("forms:check", new object[] { formID, fields });
        ActionCallContext actionCallContext = new ActionCallContext
        {
          FormItem = item
        };
        foreach (IActionDefinition definition2 in actionDefinitions)
        {
          definition = definition2;
          IActionItem item2 = this.itemRepository.CreateAction(definition2.ActionID);
          if (item2 != null)
          {
            ICheckAction actionInstance = item2.ActionInstance as ICheckAction;
            if ((actionInstance != null) && this.requirementsChecker.CheckRequirements(actionInstance.GetType()))
            {
              ReflectionUtils.SetXmlProperties(actionInstance, definition2.Paramaters, true);
              ReflectionUtils.SetXmlProperties(actionInstance, item2.GlobalParameters, true);
              actionInstance.UniqueKey = definition2.UniqueKey;
              actionInstance.ActionID = item2.ID;
              actionInstance.Execute(formID, fields, actionCallContext);
            }
          }
          else
          {
            this.logger.Warn($"Web Forms for Marketers : The '{definition2.ActionID}' action does not exist", new object());
          }
        }
      }
      catch (Exception exception)
      {
        if (definition == null)
        {
          throw;
        }
        string failureMessage = definition.GetFailureMessage(false, ID.Null);
        if (string.IsNullOrEmpty(failureMessage))
        {
          failureMessage = exception.Message;
        }
        CheckFailedArgs args = new CheckFailedArgs(formID, definition.ActionID, fields, exception)
        {
          ErrorMessage = failureMessage
        };
        CorePipeline.Run("errorCheck", args);
        if (item.IsDropoutTrackingEnabled)
        {
          this.analyticsTracker.TriggerEvent(Sitecore.WFFM.Abstractions.Analytics.IDs.FormCheckActionErrorId, "Form Check Action Error", formID, args.ErrorMessage, definition.GetTitle());
        }
        Exception exception2 = new Exception(args.ErrorMessage)
        {
          Source = exception.Source
        };
        throw exception2;
      }
    }

    public ExecuteResult ExecuteSaving(ID formID, ControlResult[] fields, IActionDefinition[] actionDefinitions, bool simpleAdapt, ID sessionID)
    {
      Assert.ArgumentNotNull(fields, nameof(fields));
      Assert.ArgumentNotNull(actionDefinitions, nameof(actionDefinitions));
      AdaptedResultList list = this.AdaptResult(fields, simpleAdapt);
      if (actionDefinitions.Length == 0)
      {
        this.logger.Warn(string.Format(this.resourceManager.GetString("NOT_DEFINED_ACTIONS"), formID), actionDefinitions);
      }
      this.RaiseEvent("forms:save", new object[] { formID, list });
      List<ExecuteResult.Failure> list2 = new List<ExecuteResult.Failure>();
      try
      {
        this.SaveFormToDatabase(formID, list);
      }
      catch (Exception exception)
      {
        this.logger.Warn("The form was not saved to database properly. Please see the details below.", this);
        //this.logger.Warn(exception.Message, exception, exception);
        throw exception;
      }
      ActionCallContext actionCallContext = new ActionCallContext();
      foreach (IActionDefinition definition in actionDefinitions)
      {
        try
        {
          IActionItem item = this.itemRepository.CreateAction(definition.ActionID);
          if (item != null)
          {
            ISaveAction actionInstance = item.ActionInstance as ISaveAction;
            if (actionInstance != null)
            {
              if (this.requirementsChecker.CheckRequirements(actionInstance.GetType()))
              {
                ReflectionUtils.SetXmlProperties(actionInstance, definition.Paramaters, true);
                ReflectionUtils.SetXmlProperties(actionInstance, item.GlobalParameters, true);
                actionInstance.UniqueKey = definition.UniqueKey;
                actionInstance.ActionID = item.ID;
                actionInstance.Execute(formID, list, actionCallContext, new object[] { sessionID });
              }
              else
              {
                this.logger.Warn($"Save action {definition.ActionID} is tried to be executed but system configuration doesn't meet with it's requirements. Recommendation is to delete this save action from fields.", new object());
              }
            }
          }
          else
          {
            this.logger.Warn($"The '{definition.ActionID}' action item does not exist", this);
          }
        }
        catch (Exception exception2)
        {
          this.logger.Warn(exception2.Message, exception2, exception2);
          string failureMessage = definition.GetFailureMessage();
          ExecuteResult.Failure failure = new ExecuteResult.Failure
          {
            IsCustom = !string.IsNullOrEmpty(failureMessage)
          };
          SaveFailedArgs args = new SaveFailedArgs(formID, list, definition.ActionID, exception2)
          {
            ErrorMessage = failure.IsCustom ? failureMessage : exception2.Message
          };
          CorePipeline.Run("errorSave", args);
          failure.ApiErrorMessage = exception2.Message;
          failure.ErrorMessage = args.ErrorMessage;
          failure.FailedAction = definition.ActionID;
          failure.StackTrace = exception2.StackTrace;
          list2.Add(failure);
        }
      }
      return new ExecuteResult { Failures = list2.ToArray() };
    }

    public void ExecuteSystemAction(ID formID, ControlResult[] list)
    {
      Item item = this.itemRepository.GetItem(FormIDs.SystemActionsRootID);
      if ((item != null) && item.HasChildren)
      {
        AdaptedResultList adaptedFields = this.AdaptResult(list, true);
        string str = $".//*[@@templateid = '{FormIDs.ActionTemplateID}']";
        foreach (Item item2 in item.Axes.SelectItems(str))
        {
          IActionItem item3 = this.itemRepository.CreateAction(item2.ID);
          try
          {
            if (item3 != null)
            {
              ISystemAction actionInstance = item3.ActionInstance as ISystemAction;
              if ((actionInstance != null) && this.requirementsChecker.CheckRequirements(actionInstance.GetType()))
              {
                ReflectionUtils.SetXmlProperties(actionInstance, item3.GlobalParameters, true);
                actionInstance.Execute(formID, adaptedFields, null, new object[] { this.analyticsTracker.SessionId });
              }
            }
            else
            {
              this.logger.Warn($"The '{item2.ID}' action item does not exist", this);
            }
          }
          catch (Exception)
          {
            ExecuteResult.Failure failure = new ExecuteResult.Failure
            {
              FailedAction = (item3 == null) ? ("Undefined action item " + item2.ID) : item3.Name
            };
            this.formContext.Failures.Add(failure);
          }
        }
      }
    }

    public IActionItem GetAcitonByUniqId(IFormItem form, string uniqid, bool saveAction)
    {
      Func<IListItemDefinition, bool> predicate = null;
      Assert.ArgumentNotNull(form, nameof(form));
      ListDefinition definition = ListDefinition.Parse(saveAction ? form.SaveActions : form.CheckActions);
      if (definition.Groups.Any<IGroupDefinition>())
      {
        if (predicate == null)
        {
          predicate = i => i.Unicid == uniqid;
        }
        IListItemDefinition definition2 = definition.Groups.First<IGroupDefinition>().ListItems.FirstOrDefault<IListItemDefinition>(predicate);
        if ((definition2 != null) && !string.IsNullOrEmpty(definition2.ItemID))
        {
          Item item = form.Database.GetItem(definition2.ItemID);
          if (item != null)
          {
            return new ActionItem(item);
          }
        }
      }
      return null;
    }

    public IEnumerable<IActionItem> GetActions(Item form) =>
        ((IEnumerable<IActionItem>)(from item in ListDefinition.Parse(form[FieldIDs.SaveActionsID]).Groups.First<IGroupDefinition>().ListItems
                                    select form.Database.GetItem(item.ItemID) into command
                                    where command != null
                                    select new ActionItem(command)).ToList<ActionItem>());

    public IEnumerable<IActionItem> GetCheckActions(Item form) =>
        (from s in this.GetActions(form)
         where s.ActionType == ActionType.Check
         select s);

    public IEnumerable<IActionItem> GetSaveActions(Item form) =>
        (from s in this.GetActions(form)
         where s.ActionType == ActionType.Save
         select s);

    public virtual void RaiseEvent(string eventName, params object[] args)
    {
      try
      {
        Event.RaiseEvent(eventName, args);
      }
      catch
      {
      }
    }

    public void SaveFormToDatabase(ID formid, AdaptedResultList fields)
    {
      this.logger.IsNull(this.analyticsTracker.Current, "Tracker.Current");
      IFormItem formItem = this.itemRepository.CreateFormItem(formid);
      if (formItem == null)
      {
        this.logger.Warn($"Form item {formid} isn't found in db", this);
      }
      else if (!formItem.IsSaveFormDataToStorage)
      {
        this.logger.Audit($"Form {formid} is not saving in db becouse it's save option is set to false", this);
      }
      else
      {
        this.logger.Audit($"Form {formid} is saving to db", this);
        FieldData[] dataArray = (from f in fields
                                 where formItem.Fields.FirstOrDefault<IFieldItem>(itemField => ((itemField.ID.ToString() == f.FieldID) && itemField.IsSaveToStorage)) != null
                                 select new FieldData
                                 {
                                   FieldId = new Guid(f.FieldID),
                                   FieldName = formItem.Fields.First<IFieldItem>(flds => (flds.ID.ToString() == f.FieldID)).Name,
                                   Data = f.Secure ? string.Empty : f.Parameters,
                                   Value = f.Secure ? string.Empty : f.Value
                                 }).ToArray<FieldData>();
        FormData form = new FormData
        {
          ContactId = this.logger.IsNull(this.analyticsTracker.CurrentContact, "Tracker.Current.Contact") ? Guid.Empty : this.analyticsTracker.CurrentContact.ContactId,
          FormID = formid.Guid,
          InteractionId = this.logger.IsNull(this.analyticsTracker.CurrentInteraction, " Tracker.Current.Interaction") ? Guid.Empty : this.analyticsTracker.CurrentInteraction.InteractionId,
          Fields = dataArray,
          Timestamp = DateTime.UtcNow
        };
        this.dataProvider.InsertFormData(form);
      }
    }
  }
}
