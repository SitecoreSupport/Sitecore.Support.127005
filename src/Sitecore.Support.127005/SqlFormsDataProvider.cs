namespace Sitecore.Support.WFFM.Analytics.Providers
{
  using Sitecore.Diagnostics;
  using Sitecore.WFFM.Abstractions.Analytics;
  using Sitecore.WFFM.Abstractions.Data;
  using Sitecore.WFFM.Abstractions.Shared;
  using Sitecore.WFFM.Analytics.Model;
  using System;
  using System.Collections.Generic;
  using System.Data;
  using System.Data.SqlClient;
  using System.Text;

  public class SqlFormsDataProvider : IWffmDataProvider
  {
    private readonly IDbConnectionProvider connectionProvider;
    private readonly string connectionString;
    private readonly ISettings settings;

    public SqlFormsDataProvider(string connectionStringName, ISettings settings, IDbConnectionProvider connectionProvider)
    {
      Assert.ArgumentNotNullOrEmpty(connectionStringName, "connectionStringName");
      Assert.ArgumentNotNull(settings, "settings");
      Assert.ArgumentNotNull(connectionProvider, "connectionProvider");
      this.connectionString = settings.GetConnectionString(connectionStringName);
      this.settings = settings;
      this.connectionProvider = connectionProvider;
    }

    [Obsolete("Use another constructor.")]
    public SqlFormsDataProvider(string connectionStringName, ISettings settings, IDbConnectionProvider connectionProvider, IRequirementsChecker requirementsChecker) : this(connectionStringName, settings, connectionProvider)
    {
    }

    private string AddParameter(IDataParameterCollection parameters, int parameterNumber, object parameterValue)
    {
      SqlParameter parameter = new SqlParameter("p" + parameterNumber, parameterValue);
      parameters.Add(parameter);
      return parameter.ParameterName;
    }

    public virtual IEnumerable<Sitecore.WFFM.Abstractions.Analytics.FormData> GetFormData(Guid formId)
    {
      if (!this.settings.IsRemoteActions)
      {
        List<Sitecore.WFFM.Abstractions.Analytics.FormData> list = new List<Sitecore.WFFM.Abstractions.Analytics.FormData>();
        bool flag = false;
        using (IDbConnection connection = this.connectionProvider.GetConnection(this.connectionString))
        {
          connection.Open();
          using (IDbCommand command = connection.CreateCommand())
          {
            command.Connection = connection;
            command.CommandText = string.Format("SELECT [Id],[FormItemId],[ContactId],[InteractionId],[TimeStamp],[Data] FROM [FormData] WHERE [FormItemId]=@p1", new object[0]);
            command.Parameters.Add(new SqlParameter("p1", formId));
            command.CommandType = CommandType.Text;
            IDataReader reader = command.ExecuteReader();
            try
            {
              while (reader.Read())
              {
                Sitecore.WFFM.Abstractions.Analytics.FormData item = new Sitecore.WFFM.Abstractions.Analytics.FormData
                {
                  Id = reader.GetGuid(0),
                  FormID = reader.GetGuid(1),
                  ContactId = reader.GetGuid(2),
                  InteractionId = reader.GetGuid(3),
                  Timestamp = reader.GetDateTime(4)
                };
                list.Add(item);
              }
            }
            catch
            {
              flag = true;
            }
            finally
            {
              reader.Close();
            }
          }
        }
        if (!flag && (list.Count > 0))
        {
          foreach (Sitecore.WFFM.Abstractions.Analytics.FormData data3 in list)
          {
            List<Sitecore.WFFM.Abstractions.Analytics.FieldData> list2 = new List<Sitecore.WFFM.Abstractions.Analytics.FieldData>();
            using (IDbConnection connection2 = this.connectionProvider.GetConnection(this.connectionString))
            {
              connection2.Open();
              using (IDbCommand command2 = connection2.CreateCommand())
              {
                command2.Connection = connection2;
                command2.CommandText = string.Format("SELECT [Id],[FieldItemId],[FieldName],[Value],[Data] FROM [FieldData] WHERE [FormId]=@p1", new object[0]);
                command2.Parameters.Add(new SqlParameter("p1", data3.Id));
                command2.CommandType = CommandType.Text;
                IDataReader reader2 = command2.ExecuteReader();
                try
                {
                  while (reader2.Read())
                  {
                    Sitecore.WFFM.Abstractions.Analytics.FieldData data4 = new Sitecore.WFFM.Abstractions.Analytics.FieldData
                    {
                      Id = new Guid(reader2["Id"].ToString()),
                      FieldId = new Guid(reader2["FieldItemId"].ToString()),
                      FieldName = reader2["FieldName"] as string,
                      Form = data3,
                      Value = reader2["Value"] as string,
                      Data = reader2["Data"] as string
                    };
                    list2.Add(data4);
                  }
                }
                catch
                {
                  flag = true;
                }
                finally
                {
                  reader2.Close();
                }
              }
            }
            if (list2.Count > 0)
            {
              data3.Fields = list2;
            }
          }
        }
        if (!flag)
        {
          return list;
        }
      }
      return new List<Sitecore.WFFM.Abstractions.Analytics.FormData>();
    }

    public virtual IEnumerable<IFormFieldStatistics> GetFormFieldsStatistics(Guid formId)
    {
      if (this.settings.IsRemoteActions)
      {
        return new List<IFormFieldStatistics>();
      }
      List<IFormFieldStatistics> list = new List<IFormFieldStatistics>();
      using (IDbConnection connection = this.connectionProvider.GetConnection(this.connectionString))
      {
        connection.Open();
        using (IDbTransaction transaction = connection.BeginTransaction())
        {
          using (IDbCommand command = connection.CreateCommand())
          {
            command.Transaction = transaction;
            command.Connection = connection;
            command.CommandText = "select FieldItemId as fieldid, max(FieldName) fieldname, COUNT(FormId) as submit_count \r\nfrom FieldData, FormData\r\nwhere FieldData.FormId=FormData.Id\r\nand FormItemId=@p1\r\ngroup by FieldItemId";
            command.Parameters.Add(new SqlParameter("p1", formId));
            command.CommandType = CommandType.Text;
            IDataReader reader = command.ExecuteReader();
            try
            {
              while (reader.Read())
              {
                FormFieldStatistics item = new FormFieldStatistics
                {
                  FieldId = new Guid(reader["fieldid"].ToString()),
                  FieldName = reader["fieldname"] as string,
                  Count = Convert.ToInt32(reader["submit_count"])
                };
                list.Add(item);
              }
            }
            finally
            {
              reader.Close();
            }
          }
          transaction.Commit();
        }
      }
      return list;
    }

    public virtual IEnumerable<IFormContactsResult> GetFormsStatisticsByContact(Guid formId, PageCriteria pageCriteria) =>
        new List<IFormContactsResult>();

    public virtual IFormStatistics GetFormStatistics(Guid formId)
    {
      int num;
      if (this.settings.IsRemoteActions)
      {
        return new FormStatistics();
      }
      using (IDbConnection connection = this.connectionProvider.GetConnection(this.connectionString))
      {
        connection.Open();
        using (IDbTransaction transaction = connection.BeginTransaction())
        {
          using (IDbCommand command = connection.CreateCommand())
          {
            command.Transaction = transaction;
            command.Connection = connection;
            command.CommandText = "SELECT COUNT(Id) AS submit_count FROM [FormData] WHERE [FormItemId]=@p1";
            command.Parameters.Add(new SqlParameter("p1", formId));
            command.CommandType = CommandType.Text;
            if (!int.TryParse((command.ExecuteScalar() ?? 0).ToString(), out num))
            {
              num = 0;
            }
          }
          transaction.Commit();
        }
      }
      return new FormStatistics
      {
        FormId = formId,
        SuccessSubmits = num
      };
    }

    public virtual void InsertFormData(Sitecore.WFFM.Abstractions.Analytics.FormData form)
    {
      bool isSubmitedRemotely = System.Environment.StackTrace.Contains("OnWffmActionEventFired");
      if (!isSubmitedRemotely)
      {
        StringBuilder builder = new StringBuilder();
        using (IDbConnection connection = this.connectionProvider.GetConnection(this.connectionString))
        {
          connection.Open();
          using (IDbTransaction transaction = connection.BeginTransaction())
          {
            using (IDbCommand command = connection.CreateCommand())
            {
              int num = 1;
              command.Transaction = transaction;
              command.Connection = connection;
              Guid parameterValue = Guid.NewGuid();
              builder.AppendFormat("INSERT INTO [FormData] ([Id],[FormItemId],[ContactId],[InteractionId],[Timestamp]) VALUES ( @{0}, @{1}, @{2}, @{3}, @{4} ) ", new object[] { this.AddParameter(command.Parameters, num++, parameterValue), this.AddParameter(command.Parameters, num++, form.FormID), this.AddParameter(command.Parameters, num++, form.ContactId), this.AddParameter(command.Parameters, num++, form.InteractionId), this.AddParameter(command.Parameters, num++, form.Timestamp) });
              if (form.Fields != null)
              {
                foreach (Sitecore.WFFM.Abstractions.Analytics.FieldData data in form.Fields)
                {
                  Guid guid2 = Guid.NewGuid();
                  object[] args = new object[] { this.AddParameter(command.Parameters, num++, guid2), this.AddParameter(command.Parameters, num++, parameterValue), this.AddParameter(command.Parameters, num++, data.FieldId), this.AddParameter(command.Parameters, num++, data.FieldName), this.AddParameter(command.Parameters, num++, data.Value), this.AddParameter(command.Parameters, num++, ((object)data.Data) ?? DBNull.Value) };
                  builder.AppendFormat("INSERT INTO [FieldData] ([Id],[FormId],[FieldItemId],[FieldName],[Value],[Data]) VALUES ( @{0}, @{1}, @{2}, @{3}, @{4}, @{5} ) ", args);
                }
              }
              command.CommandText = builder.ToString();
              command.CommandType = CommandType.Text;
              command.ExecuteNonQuery();
            }
            transaction.Commit();
          }
        }
      }
    }
  }
}
