using System.Linq;
using System.Collections.Generic;

namespace Image_Manip
{
    public static class ArgumentParser
    {
        public static (string[] /* arguments */, string[] /* non, arguments */) ParseArgs(this string[] args, string shortPrefix = "-", string longPrefix = "--") {
            // Create two lists for "proper" parameters and "other" parameters
            List<string> argsOutput = new List<string>();
            List<string> restOutput = new List<string>();
            
            // Check the args array agains the criteria
            foreach (string rawArg in args)
            {
                if (rawArg.StartsWith(longPrefix)) argsOutput.Add(new string(rawArg.Skip(2).ToArray()));
                else if (rawArg.StartsWith(shortPrefix)) argsOutput.Add(new string(rawArg.Skip(1).ToArray()));
                else restOutput.Add(rawArg); // Parameter was not recognised, so add it to the other array
            }
            
            // Call the other function
            return (argsOutput.ToArray(), restOutput.ToArray());
        }
        public static (string[] /* arguments */, string[] /* non, arguments */) ParseArgs(this string arg, string IFS, string shortPrefix = "-", string longPrefix = "--") {
            // Call the other method to do the magic
            return arg.Trim().Split(IFS).ParseArgs(shortPrefix, longPrefix);
        }
    }
}