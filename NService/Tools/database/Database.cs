using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.Data;
using System.Globalization;

namespace NService.Tools
{
    public enum DatabaseType
    {
        SqlServer,
        Oracle,
        MySql,
        MsAccess,
        Sqlite,
        PostgreSql,
        DB2
    }

    public sealed class Database
    {

        /// <summary>
        /// A delegate used for log.
        /// </summary>
        /// <param name="logMsg">The msg to write to log.</param>
        public delegate void LogHandler(DbCommand cmd);

        public event LogHandler OnLog;

        #region Private Members

        DbProviderFactory dbProvider;
        string connectionString;

        private DbCommand CreateCommandByCommandType(CommandType commandType, string commandText)
        {
            DbCommand command = dbProvider.CreateCommand();
            command.CommandType = commandType;
            command.CommandText = commandText;
            return command;
        }

        private void DoLoadDataSet(DbCommand command, DataSet dataSet, string[] tableNames)
        {
            string[] sqls = null;           //oracle不支援同時多條select語句，因此在這里迴圈調用實現
            //2013.5.2 kevin.zou 如果是oracle，如果是procedure，如果有兩個傳出類型的cursor，則要填兩次
            if (this._type == DatabaseType.Oracle)
            {
                if (command.CommandType == CommandType.StoredProcedure)
                {
                    //不用特別處理，dataAdapater會自動將所有output的cursor放到dataSet里面來
                    /*
                    List<string> tbls = new List<string>();
                    int index = 0;
                    foreach (DbParameter p in command.Parameters)
                    {
                        if (p.Direction == ParameterDirection.Output &&p.DbType== DbType.Object)
                        {
                            tbls.Add("Table" + (index == 0?"":index.ToString()));
                            index++;
                        }
                    }
                    if (tbls.Count > 1)
                    {
                        tableNames = new string[tbls.Count];
                        tbls.CopyTo(tableNames);

                    }
                     * */
                }
                else if (command.CommandType == CommandType.Text)       //用分號分割的select，循環執行多次
                {
                    sqls = command.CommandText.Split(new char[] { ';' });
                }
            }
            using (DbDataAdapter adapter = GetDataAdapter())
            {
                WriteLog(command);
                ((IDbDataAdapter)adapter).SelectCommand = command;

                if (sqls == null || sqls.Length == 1)
                {

                    string systemCreatedTableNameRoot = "Table";
                    for (int i = 0; i < tableNames.Length; i++)
                    {
                        string systemCreatedTableName = (i == 0)
                             ? systemCreatedTableNameRoot
                             : systemCreatedTableNameRoot + i;

                        adapter.TableMappings.Add(systemCreatedTableName, tableNames[i]);
                    }
                    DateTime time1 = DateTime.Now;
                    adapter.Fill(dataSet);
                    DateTime time2 = DateTime.Now;
                    TimeSpan ts = time2.Subtract(time1);
                    //Tool.Trace("[Database.DoLoadDataSet]Spend", "Seconds", ts.TotalSeconds);
                }
                else
                {
                    int index = 0;
                    foreach (string sql in sqls)
                    {
                        if (sql.Trim().Length > 0)
                        {
                            command.CommandText = sql;
                            if (index > 0)            //因為一條sql語句永遠出來Table，但是又不能重複，所以移掉先
                            {
                                adapter.TableMappings.RemoveAt(0);
                            }
                            adapter.TableMappings.Add("Table", "Table" + (index == 0 ? "" : index.ToString()));
                            index++;
                            DateTime time1 = DateTime.Now;
                            //Tool.Trace("[Database.DoLoadDataSet]", "time", time1.ToString("HH:mm:ss:fff"));

                            adapter.Fill(dataSet);
                            DateTime time2 = DateTime.Now;
                            TimeSpan ts = time2.Subtract(time1);
                            //Tool.Trace("[Database.DoLoadDataSet]Spend", "Seconds", ts.TotalSeconds, "time", time2.ToString("HH:mm:ss:fff"));
                        }
                    }
                }
            }
        }

        private object DoExecuteScalar(DbCommand command)
        {
            try
            {
                WriteLog(command);
                DateTime time1 = DateTime.Now;
                //Tool.Trace("[Database.DoExecuteScalar]", "time", time1.ToString("HH:mm:ss:fff"));
                object returnValue = command.ExecuteScalar();
                DateTime time2 = DateTime.Now;
                TimeSpan ts = time2.Subtract(time1);
                //Tool.Trace("[Database.DoExecuteScalar]Spend", "Seconds", ts.TotalSeconds, "time", time2.ToString("HH:mm:ss:fff"));

                return returnValue;
            }
            finally
            {
                CloseConnection(command);
            }
        }

        private int DoExecuteNonQuery(DbCommand command)
        {
            WriteLog(command);
            DateTime time1 = DateTime.Now;
            //Tool.Trace("[Database.DoExecuteNonQuery]", "time", time1.ToString("HH:mm:ss:fff"));
            int rowsAffected = command.ExecuteNonQuery();
            DateTime time2 = DateTime.Now;
            TimeSpan ts = time2.Subtract(time1);
            //Tool.Trace("[Database.DoExecuteNonQuery]Spend", "Seconds", ts.TotalSeconds, "time", time2.ToString("HH:mm:ss:fff"));
            return rowsAffected;
        }

        private IDataReader DoExecuteReader(DbCommand command, CommandBehavior cmdBehavior)
        {
            WriteLog(command);

            IDataReader reader = command.ExecuteReader(cmdBehavior);
            return reader;
        }

        private DbTransaction BeginTransaction(DbConnection connection)
        {
            return connection.BeginTransaction();
        }
        private IDbTransaction BeginTransaction(DbConnection connection, System.Data.IsolationLevel il)
        {
            return connection.BeginTransaction(il);
        }

        private void PrepareCommand(DbCommand command, DbConnection connection)
        {
            command.CommandTimeout = command.CommandTimeout < 60 ? 60 : command.CommandTimeout;            //20秒沒反應就斷線(默認30s，所以改了應該也沒什么用)
            //Tool.Trace("CommandTimeOut(oracle無效)", "Seconds", command.CommandTimeout);
            command.Connection = connection;
        }

        private void PrepareCommand(DbCommand command, DbTransaction transaction)
        {
            PrepareCommand(command, transaction.Connection);
            command.Transaction = transaction;
        }

        private static void ConfigureParameter(DbParameter param, string name, DbType dbType, int size, ParameterDirection direction, bool nullable, byte precision, byte scale, string sourceColumn, DataRowVersion sourceVersion, object value)
        {
            param.DbType = dbType;
            param.Size = size;
            param.Value = (value == null) ? DBNull.Value : value;
            param.Direction = direction;
            param.IsNullable = nullable;
            param.SourceColumn = sourceColumn;
            param.SourceVersion = sourceVersion;
        }

        private DbParameter CreateParameter(string name, DbType dbType, int size, ParameterDirection direction, bool nullable, byte precision, byte scale, string sourceColumn, DataRowVersion sourceVersion, object value)
        {
            DbParameter param = CreateParameter(name);
            ConfigureParameter(param, name, dbType, size, direction, nullable, precision, scale, sourceColumn, sourceVersion, value);
            return param;
        }

        private DbParameter CreateParameter(string name)
        {
            DbParameter param = dbProvider.CreateParameter();
            param.ParameterName = name;
            return param;
        }

        public DbParameter CreateParameter(string name, object value)
        {
            DbParameter param = dbProvider.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            return param;
        }

        internal void WriteLog(DbCommand command)
        {
            if (OnLog != null)
            {
                //StringBuilder sb = new StringBuilder();

                //sb.Append(string.Format("{0}\t{1}\t\r\n", command.CommandType, command.CommandText));
                //if (command.Parameters != null && command.Parameters.Count > 0)
                //{
                //    sb.Append("Parameters:\r\n");
                //    foreach (DbParameter p in command.Parameters)
                //    {
                //        sb.Append(string.Format("{0}[{2}] = {1}\r\n", p.ParameterName, DataUtils.ToString(p.DbType, p.Value), p.DbType));
                //    }
                //}
                //sb.Append("\r\n");

                OnLog(command);
            }
        }

        /// <summary>
        /// <para>Loads a <see cref="DataSet"/> from command text in a transaction.</para>
        /// </summary>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command in.</para>
        /// </param>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <param name="dataSet">
        /// <para>The <see cref="DataSet"/> to fill.</para>
        /// </param>
        /// <param name="tableNames">
        /// <para>An array of table name mappings for the <see cref="DataSet"/>.</para>
        /// </param>
        private void LoadDataSet(DbTransaction transaction, CommandType commandType, string commandText,
            DataSet dataSet, string[] tableNames)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                LoadDataSet(command, dataSet, tableNames, transaction);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" /> and returns an <see cref="IDataReader"></see> through which the result can be read.
        /// It is the responsibility of the caller to close the connection and reader when finished.</para>
        /// </summary>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>An <see cref="IDataReader"/> object.</para>
        /// </returns>        
        public IDataReader ExecuteReader(CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteReader(command);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" /> within the given 
        /// <paramref name="transaction" /> and returns an <see cref="IDataReader"></see> through which the result can be read.
        /// It is the responsibility of the caller to close the connection and reader when finished.</para>
        /// </summary>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>An <see cref="IDataReader"/> object.</para>
        /// </returns>        
        public IDataReader ExecuteReader(DbTransaction transaction, CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteReader(command, transaction);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> and adds a new <see cref="DataTable"></see> to the existing <see cref="DataSet"></see>.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The <see cref="DbCommand"/> to execute.</para>
        /// </param>
        /// <param name="dataSet">
        /// <para>The <see cref="DataSet"/> to load.</para>
        /// </param>
        /// <param name="tableName">
        /// <para>The name for the new <see cref="DataTable"/> to add to the <see cref="DataSet"/>.</para>
        /// </param>        
        /// <exception cref="System.ArgumentNullException">Any input parameter was <see langword="null"/> (<b>Nothing</b> in Visual Basic)</exception>
        /// <exception cref="System.ArgumentException">tableName was an empty string</exception>
        private void LoadDataSet(DbCommand command, DataSet dataSet, string tableName)
        {
            LoadDataSet(command, dataSet, new string[] { tableName });
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> within the given <paramref name="transaction" /> and adds a new <see cref="DataTable"></see> to the existing <see cref="DataSet"></see>.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The <see cref="DbCommand"/> to execute.</para>
        /// </param>
        /// <param name="dataSet">
        /// <para>The <see cref="DataSet"/> to load.</para>
        /// </param>
        /// <param name="tableName">
        /// <para>The name for the new <see cref="DataTable"/> to add to the <see cref="DataSet"/>.</para>
        /// </param>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>        
        /// <exception cref="System.ArgumentNullException">Any input parameter was <see langword="null"/> (<b>Nothing</b> in Visual Basic).</exception>
        /// <exception cref="System.ArgumentException">tableName was an empty string.</exception>
        private void LoadDataSet(DbCommand command, DataSet dataSet, string tableName, DbTransaction transaction)
        {
            LoadDataSet(command, dataSet, new string[] { tableName }, transaction);
        }

        /// <summary>
        /// <para>Loads a <see cref="DataSet"/> from a <see cref="DbCommand"/>.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command to execute to fill the <see cref="DataSet"/>.</para>
        /// </param>
        /// <param name="dataSet">
        /// <para>The <see cref="DataSet"/> to fill.</para>
        /// </param>
        /// <param name="tableNames">
        /// <para>An array of table name mappings for the <see cref="DataSet"/>.</para>
        /// </param>
        private void LoadDataSet(DbCommand command, DataSet dataSet, string[] tableNames)
        {

            using (DbConnection connection = GetConnection())
            {
                PrepareCommand(command, connection);
                DoLoadDataSet(command, dataSet, tableNames);
            }
        }

        /// <summary>
        /// <para>Loads a <see cref="DataSet"/> from a <see cref="DbCommand"/> in  a transaction.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command to execute to fill the <see cref="DataSet"/>.</para>
        /// </param>
        /// <param name="dataSet">
        /// <para>The <see cref="DataSet"/> to fill.</para>
        /// </param>
        /// <param name="tableNames">
        /// <para>An array of table name mappings for the <see cref="DataSet"/>.</para>
        /// </param>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command in.</para>
        /// </param>
        private void LoadDataSet(DbCommand command, DataSet dataSet, string[] tableNames, DbTransaction transaction)
        {
            PrepareCommand(command, transaction);
            DoLoadDataSet(command, dataSet, tableNames);
        }

        /// <summary>
        /// <para>Loads a <see cref="DataSet"/> from command text.</para>
        /// </summary>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <param name="dataSet">
        /// <para>The <see cref="DataSet"/> to fill.</para>
        /// </param>
        /// <param name="tableNames">
        /// <para>An array of table name mappings for the <see cref="DataSet"/>.</para>
        /// </param>
        private void LoadDataSet(CommandType commandType, string commandText, DataSet dataSet, string[] tableNames)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                LoadDataSet(command, dataSet, tableNames);
            }
        }

        #endregion

        #region Close Connection

        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="command">The command.</param>
        public void CloseConnection(DbCommand command)
        {
            if (command != null && command.Connection.State != ConnectionState.Closed)
            {
                if (command.Transaction == null)
                {
                    CloseConnection(command.Connection);
                    command.Dispose();
                }
            }
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="conn">The conn.</param>
        public void CloseConnection(DbConnection conn)
        {
            if (conn != null && conn.State != ConnectionState.Closed)
            {
                conn.Close();
                conn.Dispose();
            }
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="tran">The tran.</param>
        public void CloseConnection(DbTransaction tran)
        {
            if (tran.Connection != null)
            {
                CloseConnection(tran.Connection);
                tran.Dispose();
            }
        }

        #endregion

        public bool IsSameConnection(Database db)
        {
            return db != null && this.connectionString == db.connectionString;
        }

        #region Constructors

        DatabaseType _type;

        public DatabaseType DBType
        {
            get
            {
                return _type;
            }
        }

        public Database(string type, string connectionString)
            : this(type == "oracle" || type == "$oracle" ? DatabaseType.Oracle : DatabaseType.SqlServer, type.StartsWith("$") ? NService.Tools.EncryptHelper.Decrypt(connectionString) : connectionString)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Database"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="connectionString">The connection string.</param>
        public Database(DatabaseType type, string connectionString)
        {
            _type = type;
            switch (type)
            {
                case DatabaseType.Oracle:
                    this.dbProvider = DbProviderFactories.GetFactory("System.Data.OracleClient");
                    break;
                case DatabaseType.SqlServer:
                    this.dbProvider = DbProviderFactories.GetFactory("System.Data.SqlClient");
                    break;
                /*
                case DatabaseType.MsAccess:
                    this.dbProvider = DbProviders.DbProviderFactory.CreateDbProvider(null, typeof(DbProviders.MsAccess.AccessDbProvider).ToString(), connectionString);
                    break;
                case DatabaseType.MySql:
                    this.dbProvider = DbProviders.DbProviderFactory.CreateDbProvider("NBearLite.AdditionalDbProviders", "NBearLite.DbProviders.MySql.MySqlDbProvider", connectionString);
                    break;
                case DatabaseType.Sqlite:
                    this.dbProvider = DbProviders.DbProviderFactory.CreateDbProvider("NBearLite.AdditionalDbProviders", "NBearLite.DbProviders.Sqlite.SqliteDbProvider", connectionString);
                    break;
                case DatabaseType.PostgreSql:
                    this.dbProvider = DbProviders.DbProviderFactory.CreateDbProvider("NBearLite.AdditionalDbProviders", "NBearLite.DbProviders.PostgreSql.PostgreSqlDbProvider", connectionString);
                    break;
                case DatabaseType.DB2:
                    this.dbProvider = DbProviders.DbProviderFactory.CreateDbProvider("NBearLite.AdditionalDbProviders", "NBearLite.DbProviders.DB2.DB2DbProvider", connectionString);
                    break;
                */
                default:
                    throw new NotSupportedException("Unknow DatabaseType.");
            }
            this.connectionString = connectionString;
        }

        #endregion

        #region Public Methods

        #region Factory Methods

        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <returns></returns>
        public DbConnection GetConnection()
        {
            return CreateConnection();
        }

        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <param name="tryOpen">if set to <c>true</c> [try open].</param>
        /// <returns></returns>
        public DbConnection GetConnection(bool tryOpen)
        {
            return CreateConnection(tryOpen);
        }

        /// <summary>
        /// <para>When overridden in a derived class, gets the connection for this database.</para>
        /// <seealso cref="DbConnection"/>        
        /// </summary>
        /// <returns>
        /// <para>The <see cref="DbConnection"/> for this database.</para>
        /// </returns>
        public DbConnection CreateConnection()
        {
            DbConnection newConnection = dbProvider.CreateConnection();
            newConnection.ConnectionString = connectionString;

            return newConnection;
        }

        /// <summary>
        /// <para>When overridden in a derived class, gets the connection for this database.</para>
        /// <seealso cref="DbConnection"/>        
        /// </summary>
        /// <returns>
        /// <para>The <see cref="DbConnection"/> for this database.</para>
        /// </returns>
        public DbConnection CreateConnection(bool tryOpenning)
        {
            if (!tryOpenning)
            {
                return CreateConnection();
            }

            DbConnection connection = null;
            try
            {
                connection = CreateConnection();
                connection.Open();
            }
            catch (DataException)
            {
                CloseConnection(connection);

                throw;
            }

            return connection;
        }

        /// <summary>
        /// <para>When overridden in a derived class, creates a <see cref="DbCommand"/> for a stored procedure.</para>
        /// </summary>
        /// <param name="storedProcedureName"><para>The name of the stored procedure.</para></param>
        /// <returns><para>The <see cref="DbCommand"/> for the stored procedure.</para></returns>       
        public DbCommand GetStoredProcCommand(string storedProcedureName)
        {
            return CreateCommandByCommandType(CommandType.StoredProcedure, storedProcedureName);
        }

        /// <summary>
        /// <para>When overridden in a derived class, creates an <see cref="DbCommand"/> for a SQL query.</para>
        /// </summary>
        /// <param name="query"><para>The text of the query.</para></param>        
        /// <returns><para>The <see cref="DbCommand"/> for the SQL query.</para></returns>        
        public DbCommand GetSqlStringCommand(string query)
        {
            return CreateCommandByCommandType(CommandType.Text, query);
        }

        /// <summary>
        /// Gets a DbDataAdapter with Standard update behavior.
        /// </summary>
        /// <returns>A <see cref="DbDataAdapter"/>.</returns>
        /// <seealso cref="DbDataAdapter"/>
        public DbDataAdapter GetDataAdapter()
        {
            return dbProvider.CreateDataAdapter();
        }

        #endregion

        #region Basic Execute Methods

        /// <summary>
        /// <para>Executes the <paramref name="command"/> and returns the results in a new <see cref="DataSet"/>.</para>
        /// </summary>
        /// <param name="command"><para>The <see cref="DbCommand"/> to execute.</para></param>
        /// <returns>A <see cref="DataSet"/> with the results of the <paramref name="command"/>.</returns>        
        public DataSet ExecuteDataSet(DbCommand command)
        {
            DataSet dataSet = new DataSet();
            dataSet.Locale = CultureInfo.InvariantCulture;
            LoadDataSet(command, dataSet, "Table");
            return dataSet;
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> as part of the <paramref name="transaction" /> and returns the results in a new <see cref="DataSet"/>.</para>
        /// </summary>
        /// <param name="command"><para>The <see cref="DbCommand"/> to execute.</para></param>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <returns>A <see cref="DataSet"/> with the results of the <paramref name="command"/>.</returns>        
        public DataSet ExecuteDataSet(DbCommand command, DbTransaction transaction)
        {
            DataSet dataSet = new DataSet();
            dataSet.Locale = CultureInfo.InvariantCulture;
            LoadDataSet(command, dataSet, "Table", transaction);
            return dataSet;
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" /> and returns the results in a new <see cref="DataSet"/>.</para>
        /// </summary>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>A <see cref="DataSet"/> with the results of the <paramref name="commandText"/>.</para>
        /// </returns>
        public DataSet ExecuteDataSet(CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteDataSet(command);
            }
        }

        public DataSet ExecuteDataSet(string commandText)
        {
            return ExecuteDataSet(CommandType.Text, commandText);
        }


        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> as part of the given <paramref name="transaction" /> and returns the results in a new <see cref="DataSet"/>.</para>
        /// </summary>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>A <see cref="DataSet"/> with the results of the <paramref name="commandText"/>.</para>
        /// </returns>
        public DataSet ExecuteDataSet(DbTransaction transaction, CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteDataSet(command, transaction);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command that contains the query to execute.</para>
        /// </param>
        /// <returns>
        /// <para>The first column of the first row in the result set.</para>
        /// </returns>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public object ExecuteScalar(DbCommand command)
        {
            using (DbConnection connection = GetConnection(true))
            {
                PrepareCommand(command, connection);
                return DoExecuteScalar(command);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> within a <paramref name="transaction" />, and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command that contains the query to execute.</para>
        /// </param>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <returns>
        /// <para>The first column of the first row in the result set.</para>
        /// </returns>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public object ExecuteScalar(DbCommand command, DbTransaction transaction)
        {
            PrepareCommand(command, transaction);
            return DoExecuteScalar(command);
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" />  and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.</para>
        /// </summary>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>The first column of the first row in the result set.</para>
        /// </returns>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public object ExecuteScalar(CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteScalar(command);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" /> 
        /// within the given <paramref name="transaction" /> and returns the first column of the first row in the result set returned by the query. Extra columns or rows are ignored.</para>
        /// </summary>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>The first column of the first row in the result set.</para>
        /// </returns>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public object ExecuteScalar(DbTransaction transaction, CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteScalar(command, transaction);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> and returns the number of rows affected.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command that contains the query to execute.</para>
        /// </param>       
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public int ExecuteNonQuery(DbCommand command)
        {
            using (DbConnection connection = GetConnection(true))
            {
                PrepareCommand(command, connection);
                return DoExecuteNonQuery(command);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> within the given <paramref name="transaction" />, and returns the number of rows affected.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command that contains the query to execute.</para>
        /// </param>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public int ExecuteNonQuery(DbCommand command, DbTransaction transaction)
        {
            PrepareCommand(command, transaction);
            return DoExecuteNonQuery(command);
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" /> and returns the number of rows affected.</para>
        /// </summary>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>The number of rows affected.</para>
        /// </returns>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public int ExecuteNonQuery(CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteNonQuery(command);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="commandText"/> interpreted as specified by the <paramref name="commandType" /> as part of the given <paramref name="transaction" /> and returns the number of rows affected.</para>
        /// </summary>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <param name="commandType">
        /// <para>One of the <see cref="CommandType"/> values.</para>
        /// </param>
        /// <param name="commandText">
        /// <para>The command text to execute.</para>
        /// </param>
        /// <returns>
        /// <para>The number of rows affected</para>
        /// </returns>
        /// <seealso cref="IDbCommand.ExecuteScalar"/>
        public int ExecuteNonQuery(DbTransaction transaction, CommandType commandType, string commandText)
        {
            using (DbCommand command = CreateCommandByCommandType(commandType, commandText))
            {
                return ExecuteNonQuery(command, transaction);
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> and returns an <see cref="IDataReader"></see> through which the result can be read.
        /// It is the responsibility of the caller to close the connection and reader when finished.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command that contains the query to execute.</para>
        /// </param>
        /// <returns>
        /// <para>An <see cref="IDataReader"/> object.</para>
        /// </returns>        
        public IDataReader ExecuteReader(DbCommand command)
        {
            DbConnection connection = GetConnection(true);
            PrepareCommand(command, connection);

            try
            {
                return DoExecuteReader(command, CommandBehavior.CloseConnection);
            }
            catch (DataException)
            {
                CloseConnection(connection);

                throw;
            }
        }

        /// <summary>
        /// <para>Executes the <paramref name="command"/> within a transaction and returns an <see cref="IDataReader"></see> through which the result can be read.
        /// It is the responsibility of the caller to close the connection and reader when finished.</para>
        /// </summary>
        /// <param name="command">
        /// <para>The command that contains the query to execute.</para>
        /// </param>
        /// <param name="transaction">
        /// <para>The <see cref="IDbTransaction"/> to execute the command within.</para>
        /// </param>
        /// <returns>
        /// <para>An <see cref="IDataReader"/> object.</para>
        /// </returns>        
        public IDataReader ExecuteReader(DbCommand command, DbTransaction transaction)
        {
            PrepareCommand(command, transaction);
            return DoExecuteReader(command, CommandBehavior.Default);
        }

        #endregion

        #region ASP.NET 1.1 style Transactions

        /// <summary>
        /// Begins the transaction.
        /// </summary>
        /// <returns></returns>
        public DbTransaction BeginTransaction()
        {
            return GetConnection(true).BeginTransaction();
        }

        /// <summary>
        /// Begins the transaction.
        /// </summary>
        /// <param name="il">The il.</param>
        /// <returns></returns>
        public DbTransaction BeginTransaction(System.Data.IsolationLevel il)
        {
            return GetConnection(true).BeginTransaction(il);
        }

        #endregion

        #region DbCommand Parameter Methods

        /// <summary>
        /// Adds a new In <see cref="DbParameter"/> object to the given <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The command to add the parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="dbType"><para>One of the <see cref="DbType"/> values.</para></param>
        /// <param name="size"><para>The maximum size of the data within the column.</para></param>
        /// <param name="direction"><para>One of the <see cref="ParameterDirection"/> values.</para></param>
        /// <param name="nullable"><para>Avalue indicating whether the parameter accepts <see langword="null"/> (<b>Nothing</b> in Visual Basic) values.</para></param>
        /// <param name="precision"><para>The maximum number of digits used to represent the <paramref name="value"/>.</para></param>
        /// <param name="scale"><para>The number of decimal places to which <paramref name="value"/> is resolved.</para></param>
        /// <param name="sourceColumn"><para>The name of the source column mapped to the DataSet and used for loading or returning the <paramref name="value"/>.</para></param>
        /// <param name="sourceVersion"><para>One of the <see cref="DataRowVersion"/> values.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>       
        public void AddParameter(DbCommand command, string name, DbType dbType, int size, ParameterDirection direction, bool nullable, byte precision, byte scale, string sourceColumn, DataRowVersion sourceVersion, object value)
        {
            DbParameter parameter = CreateParameter(name, dbType == DbType.Object ? DbType.String : dbType, size, direction, nullable, precision, scale, sourceColumn, sourceVersion, value);
            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// <para>Adds a new instance of a <see cref="DbParameter"/> object to the command.</para>
        /// </summary>
        /// <param name="command">The command to add the parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="dbType"><para>One of the <see cref="DbType"/> values.</para></param>        
        /// <param name="direction"><para>One of the <see cref="ParameterDirection"/> values.</para></param>                
        /// <param name="sourceColumn"><para>The name of the source column mapped to the DataSet and used for loading or returning the <paramref name="value"/>.</para></param>
        /// <param name="sourceVersion"><para>One of the <see cref="DataRowVersion"/> values.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>    
        public void AddParameter(DbCommand command, string name, DbType dbType, ParameterDirection direction, string sourceColumn, DataRowVersion sourceVersion, object value)
        {
            AddParameter(command, name, dbType, 0, direction, false, 0, 0, sourceColumn, sourceVersion, value);
        }

        /// <summary>
        /// Adds a new Out <see cref="DbParameter"/> object to the given <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The command to add the out parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="dbType"><para>One of the <see cref="DbType"/> values.</para></param>        
        /// <param name="size"><para>The maximum size of the data within the column.</para></param>        
        public void AddOutParameter(DbCommand command, string name, DbType dbType, int size)
        {
            AddParameter(command, name, dbType, size, ParameterDirection.Output, true, 0, 0, String.Empty, DataRowVersion.Default, DBNull.Value);
        }

        /// <summary>
        /// Adds a new In <see cref="DbParameter"/> object to the given <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The command to add the in parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="dbType"><para>One of the <see cref="DbType"/> values.</para></param>                
        /// <remarks>
        /// <para>This version of the method is used when you can have the same parameter object multiple times with different values.</para>
        /// </remarks>        
        public void AddInParameter(DbCommand command, string name, DbType dbType)
        {
            AddParameter(command, name, dbType, ParameterDirection.Input, String.Empty, DataRowVersion.Default, null);
        }

        /// <summary>
        /// Adds a new In <see cref="DbParameter"/> object to the given <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The commmand to add the parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="dbType"><para>One of the <see cref="DbType"/> values.</para></param>                
        /// <param name="value"><para>The value of the parameter.</para></param>      
        public void AddInParameter(DbCommand command, string name, DbType dbType, object value)
        {
            AddParameter(command, name, dbType, ParameterDirection.Input, String.Empty, DataRowVersion.Default, value);
        }

        /// <summary>
        /// Adds a new In <see cref="DbParameter"/> object to the given <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The commmand to add the parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>      
        public void AddInParameter(DbCommand command, string name, object value)
        {
            AddParameter(command, name, DbType.Object, ParameterDirection.Input, String.Empty, DataRowVersion.Default, value);
        }

        /// <summary>
        /// Adds a new In <see cref="DbParameter"/> object to the given <paramref name="command"/>.
        /// </summary>
        /// <param name="command">The command to add the parameter.</param>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="dbType"><para>One of the <see cref="DbType"/> values.</para></param>                
        /// <param name="sourceColumn"><para>The name of the source column mapped to the DataSet and used for loading or returning the value.</para></param>
        /// <param name="sourceVersion"><para>One of the <see cref="DataRowVersion"/> values.</para></param>
        public void AddInParameter(DbCommand command, string name, DbType dbType, string sourceColumn, DataRowVersion sourceVersion)
        {
            AddParameter(command, name, dbType, 0, ParameterDirection.Input, true, 0, 0, sourceColumn, sourceVersion, null);
        }

        #endregion

        #endregion
    }
}
