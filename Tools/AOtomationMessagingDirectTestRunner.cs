using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class AOtomationMessagingDirectTestRunner
{
    private const string TestClassAttribute =
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute";
    private const string TestMethodAttribute =
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute";
    private const string TestInitializeAttribute =
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute";
    private const string TestCleanupAttribute =
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute";
    private const string IgnoreAttribute =
        "Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute";

    private static int Main(string[] arguments)
    {
        if (arguments.Length == 0 || !File.Exists(arguments[0]))
        {
            Console.Error.WriteLine("ERROR: test assembly path is missing or invalid.");
            return 2;
        }

        string assemblyPath = Path.GetFullPath(arguments[0]);
        string[] filters = arguments.Skip(1)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        Environment.CurrentDirectory = Path.GetDirectoryName(assemblyPath);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromTestDirectory;

        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        var failures = new List<string>();
        int passed = 0;
        int skipped = 0;

        foreach (Type testClass in assembly.GetTypes()
                     .Where(type => HasAttribute(type, TestClassAttribute))
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            MethodInfo[] methods = testClass.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo initialize = methods.SingleOrDefault(
                method => HasAttribute(method, TestInitializeAttribute));
            MethodInfo cleanup = methods.SingleOrDefault(
                method => HasAttribute(method, TestCleanupAttribute));

            foreach (MethodInfo test in methods
                         .Where(method => HasAttribute(method, TestMethodAttribute))
                         .OrderBy(method => method.Name, StringComparer.Ordinal))
            {
                string testName = testClass.FullName + "." + test.Name;
                if (!MatchesFilter(testName, filters)
                    || HasAttribute(testClass, IgnoreAttribute)
                    || HasAttribute(test, IgnoreAttribute))
                {
                    skipped++;
                    continue;
                }

                object instance = Activator.CreateInstance(testClass);
                Exception failure = null;
                try
                {
                    if (initialize != null)
                    {
                        initialize.Invoke(instance, null);
                    }

                    test.Invoke(instance, null);
                }
                catch (TargetInvocationException exception)
                {
                    failure = exception.InnerException ?? exception;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    if (cleanup != null)
                    {
                        try
                        {
                            cleanup.Invoke(instance, null);
                        }
                        catch (TargetInvocationException exception)
                        {
                            if (failure == null)
                            {
                                failure = exception.InnerException ?? exception;
                            }
                        }
                    }
                }

                if (failure == null)
                {
                    passed++;
                }
                else
                {
                    failures.Add(testName + ": " + failure);
                }
            }
        }

        foreach (string failure in failures)
        {
            Console.Error.WriteLine("FAIL " + failure);
        }

        Console.WriteLine(
            "Direct MSTest summary: passed={0} failed={1} skipped={2}",
            passed,
            failures.Count,
            skipped);
        return failures.Count == 0 ? 0 : 1;
    }

    private static bool MatchesFilter(string testName, string[] filters)
    {
        if (filters.Length == 0)
        {
            return true;
        }

        return filters.Any(
            filter =>
            {
                int separator = filter.LastIndexOf(':');
                string value = separator >= 0 ? filter.Substring(separator + 1) : filter;
                return testName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
            });
    }

    private static bool HasAttribute(MemberInfo member, string fullName)
    {
        return member.GetCustomAttributesData().Any(
            attribute => string.Equals(
                attribute.AttributeType.FullName,
                fullName,
                StringComparison.Ordinal));
    }

    private static Assembly ResolveFromTestDirectory(object sender, ResolveEventArgs arguments)
    {
        string directory = Environment.CurrentDirectory;
        string path = Path.Combine(directory, new AssemblyName(arguments.Name).Name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
}
