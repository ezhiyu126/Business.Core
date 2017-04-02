/*==================================
             ########
            ##########

             ########
            ##########
          ##############
         #######  #######
        ######      ######
        #####        #####
        ####          ####
        ####   ####   ####
        #####  ####  #####
         ################
          ##############
==================================*/

namespace Business.Auth
{
    using Business.Attributes;
    using Result;

    public interface IInterceptor<Result> : Castle.DynamicProxy.IInterceptor, System.IDisposable
        where Result : IResult, new()
    {
        System.Action<LoggerData> WriteLogAsync { get; set; }

        System.Collections.Concurrent.ConcurrentDictionary<string, MetaData> MetaData { get; set; }

        dynamic Business { get; set; }
    }

    public class Interceptor<Result> : IInterceptor<Result>
        where Result : IResult, new()
    {
        public virtual void Intercept(Castle.DynamicProxy.IInvocation invocation)
        {
            var startTime = new System.Diagnostics.Stopwatch();
            startTime.Start();
            var meta = this.MetaData[invocation.Method.Name];
            var methodName = meta.FullName;
            var argsObj = invocation.Arguments;
            var argsObjLog = new System.Collections.Generic.List<System.Tuple<string, dynamic, MetaLogger>>(argsObj.Length);
            var logType = LoggerType.Record;
            var metaLogger = meta.MetaLogger;
            //==================================//
            var iArgGroup = Bind.GetCommandGroupDefault(invocation.Method.Name);
            var iArgs = Bind.GetIArgs(meta.IArgs, argsObj, iArgGroup);
            if (0 < iArgs.Count)
            {
                var group = iArgs[meta.IArgs[0].Position].Group;
                if (!System.String.IsNullOrEmpty(group)) { iArgGroup = group; }
            }

            var args = meta.ArgAttrs[iArgGroup];

            try
            {
                foreach (var item in args.Args)
                {
                    IResult result = null;
                    var value = argsObj[item.Position];
                    var iArgIn = item.HasIArg ? iArgs[item.Position].In : null;
                    argsObjLog.Add(new System.Tuple<string, dynamic, MetaLogger>(item.Name, item.HasIArg ? iArgIn : value, item.MetaLogger));

                    var iArgHasString = !System.Object.Equals(null, iArgIn) && typeof(System.String).Equals(iArgIn.GetType());
                    var trim = false;

                    for (int i = 0; i < item.ArgAttr.Count; i++)
                    {
                        var argAttr = item.ArgAttr[i];

                        if (item.HasIArg)
                        {
                            trim = (args.CommandAttr.TrimChar || argAttr.TrimChar) && iArgHasString;

                            if (trim)
                            {
                                iArgIn = System.Convert.ToString(iArgIn).Trim();
                                if (!System.Object.Equals(iArgs[item.Position].In, iArgIn))
                                {
                                    iArgs[item.Position].In = iArgIn;
                                }
                            }

                            result = argAttr.Proces(iArgIn, item.IArgOutType, methodName, item.Name, this.Business);
                        }
                        else
                        {
                            trim = item.HasString && (args.CommandAttr.TrimChar || argAttr.TrimChar) && null != value;

                            if (trim)
                            {
                                value = System.Convert.ToString(value).Trim();
                                argsObj[item.Position] = value;
                            }

                            result = argAttr.Proces(value, item.Type, methodName, item.Name, this.Business);
                        }

                        if (1 > result.State)
                        {
                            invocation.ReturnValue = Bind.GetReturnValue<Result>(result.State, result.Message, meta); logType = LoggerType.Error; return;
                        }

                        //========================================//

                        if (result.HasData)
                        {
                            if (!item.HasIArg)
                            {
                                argsObj[item.Position] = result.Data;
                            }
                            else
                            {
                                if (i < item.ArgAttr.Count - 1)
                                {
                                    iArgIn = result.Data;
                                }
                                else
                                {
                                    iArgs[item.Position].Out = result.Data;
                                }
                            }
                        }
                    }

                    //========================================//
                    object currentValue = item.HasIArg ? (null != result && result.HasData) ? result.Data : iArgs[item.Position].Out : (null != result && result.HasData) ? result.Data : value;
                    //========================================//
                    //item.HasIArg && 
                    if (0 == item.ArgAttrChild.Count || null == currentValue)
                    {
                        continue;
                    }

                    var trimIArg = false;

                    foreach (var argAttrChild in item.ArgAttrChild)
                    {
                        if (argAttrChild.Trim || 0 < argAttrChild.ArgAttr.Count)
                        {
                            var memberValue = argAttrChild.MemberAccessorGet(currentValue);
                            if (argAttrChild.Trim && null != memberValue)
                            {
                                memberValue = System.Convert.ToString(memberValue).Trim();
                                argAttrChild.MemberAccessorSet(currentValue, memberValue);
                                if (!trimIArg) { trimIArg = !trimIArg; }
                            }

                            //========================================//

                            foreach (var argAttr in argAttrChild.ArgAttr)
                            {
                                result = argAttr.Proces(memberValue, argAttrChild.Type, methodName, argAttrChild.FullName, this.Business);
                                if (0 >= result.State) { invocation.ReturnValue = Bind.GetReturnValue<Result>(result.State, result.Message, meta); logType = LoggerType.Error; return; }
                                if (result.HasData)
                                {
                                    if (!argAttrChild.HasIArg)
                                    {
                                        argAttrChild.MemberAccessorSet(currentValue, result.Data);
                                        if (!trimIArg) { trimIArg = !trimIArg; }
                                    }
                                }
                            }
                        }
                    }

                    //========================================//

                    if (item.HasIArg && trimIArg)
                    {
                        iArgs[item.Position].Out = currentValue;
                    }
                }

                //===============================//
                startTime.Restart();
                invocation.Proceed();
            }
            catch (System.Exception ex)
            {
                invocation.ReturnValue = Bind.GetReturnValue<Result>(0, System.Convert.ToString(ex), meta); logType = LoggerType.Exception;
            }
            finally
            {
                startTime.Stop();
                var total = Extensions.Help.Scale(startTime.Elapsed.TotalSeconds, 3);

                if (null != this.WriteLogAsync)
                {
                    if (meta.HasIResult && 0 > ((IResult)invocation.ReturnValue).State)
                    {
                        logType = LoggerType.Error;
                    }

                    var logObjs = Logger(logType, metaLogger, argsObjLog, out bool canWrite, out bool canResult);

                    if (canWrite)
                    {
                        this.WriteLogAsync.BeginInvoke(new LoggerData { Type = logType, Value = 0 == logObjs.Count ? null : logObjs, Result = canResult ? invocation.ReturnValue : null, Time = total, Member = methodName, Group = args.CommandAttr.Group }, null, null);
                    }
                }
            }
        }

        static void Logger(LoggerAttribute logAttr, LoggerAttribute argLogAttr, System.Collections.Generic.Dictionary<string, dynamic> logObjs, string name, object value)
        {
            switch (logAttr.CanValue)
            {
                case LoggerAttribute.ValueMode.All:
                    logObjs.Add(name, value);
                    break;
                case LoggerAttribute.ValueMode.Select:
                    if (null != argLogAttr && argLogAttr.CanWrite)
                    {
                        logObjs.Add(name, value);
                    }
                    break;
                default: break;
            }
        }

        static System.Collections.Generic.Dictionary<string, dynamic> Logger(LoggerType logType, MetaLogger metaLogger, System.Collections.Generic.List<System.Tuple<string, dynamic, MetaLogger>> argsObjLog, out bool canWrite, out bool canResult)
        {
            canWrite = canResult = false;
            var logObjs = new System.Collections.Generic.Dictionary<string, dynamic>(argsObjLog.Count);

            switch (logType)
            {
                case LoggerType.Record:
                    if (metaLogger.RecordAttr.CanWrite)
                    {
                        foreach (var log in argsObjLog)
                        {
                            Logger(metaLogger.RecordAttr, log.Item3.RecordAttr, logObjs, log.Item1, log.Item2);
                        }
                        canWrite = true;

                        if (metaLogger.RecordAttr.CanResult)
                        {
                            canResult = true;
                        }
                    }
                    break;
                case LoggerType.Error:
                    if (metaLogger.ErrorAttr.CanWrite)
                    {
                        foreach (var log in argsObjLog)
                        {
                            Logger(metaLogger.ErrorAttr, log.Item3.ErrorAttr, logObjs, log.Item1, log.Item2);
                        }
                        canWrite = true;

                        if (metaLogger.ErrorAttr.CanResult)
                        {
                            canResult = true;
                        }
                    }
                    break;
                case LoggerType.Exception:
                    if (metaLogger.ExceptionAttr.CanWrite)
                    {
                        foreach (var log in argsObjLog)
                        {
                            Logger(metaLogger.ExceptionAttr, log.Item3.ExceptionAttr, logObjs, log.Item1, log.Item2);
                        }
                        canWrite = true;

                        if (metaLogger.ExceptionAttr.CanResult)
                        {
                            canResult = true;
                        }
                    }
                    break;
            }

            return logObjs;
        }

        public System.Collections.Concurrent.ConcurrentDictionary<string, MetaData> MetaData { get; set; }

        public System.Action<LoggerData> WriteLogAsync { get; set; }

        public dynamic Business { get; set; }

        public void Dispose()
        {

        }
    }
}