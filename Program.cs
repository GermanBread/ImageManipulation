// System
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

// SixLabors
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing.Processing;

namespace Image_Manip
{
    class Program
    {
        static void Main(string[] rawArgs) {
            #region Argument parsing
            
            (string[] switches, string[] files) = rawArgs.ParseArgs();

            if (files.Length == 0) goto Help;
            if (switches.Length == 0) goto Help;

            // Variables
            string input = files[0]; // Input file
            string output = Path.GetFileNameWithoutExtension(files[0]) +" modified"; // Output file
            
            List<Task<Rgba32[]>> conversionTasks = null;
            
            Image<Rgba32> image = null; // The input file
            Image<Rgba32> borderImage = null; // The secondary image
            Image<Rgba32> borderCanvas = null; // The border canvas
            Image<Rgba32> gifCanvas = null; // used for stitching together gifs
            
            Random RNG = new Random();
            
            int borderWidth = 50;
            int innerBorderWidth = 15;
            int rgbOffset = RNG.Next(0, 90);
            int frameCount = 24;
            int animationSpeed = 1;
            float blurStrength = 5f;
            float backgroundDim = 1f;
            Rgba32? borderColor = null;
            bool discardPixels = false;
            bool animated = false;
            bool reversedAnimation = false;

            #endregion

            #region Jump logic
            
            // Sometimes if-statements and gotos do it better
            if (switches.Contains("n")) goto Brighten;
            else if (switches.Contains("f")) goto Funkyfy;
            else if (switches.Contains("b")) goto Border;
            else if (switches.Contains("c")) goto CircularBorder;
            else goto Help;

            #endregion
            
            #region Help

            Help:
            Console.WriteLine("Help menu");
            Console.WriteLine(new string('-', Console.WindowWidth));
            Console.WriteLine("Usage");
            Console.WriteLine("executable [FILE] [SWITCH] [PARAMETERS]");
            Console.WriteLine("Switches");
            Console.WriteLine("-n\tBrightness normalization");
            Console.WriteLine("-f\tApply some effects to get a cool-looking image");
            Console.WriteLine("-b\tAdd a cool border around the image");
            Console.WriteLine("-c\tAdd a cool circular border around the image");
            Console.WriteLine();
            Console.WriteLine("Parameters");
            Console.WriteLine("\t-p:w=\tWidth in pixels (example -p:w=5 [must be positive])");
            Console.WriteLine("\t-p:r=\tInner width (example -p:w=5)");
            Console.WriteLine("\t-p:c=\tColor in hex (example -p:c=12345678 [the last two digits are optional])");
            Console.WriteLine("\t-p:i=\tBorder image (example -p:i=image.png)");
            Console.WriteLine("\t-p:a=\tBlur amount (example -p:a=1,2)");
            Console.WriteLine("\t-p:b=\tBackground dim [for images with transparency] (example -p:a=0,5 [from 0 to 1])");
            Console.WriteLine("\t-p:d\tDiscard excess pixels [only effective for circular borders]");
            Console.WriteLine("\t-p:an\tAnimate border [only effective for circular borders]");
            Console.WriteLine("\t-p:f=\tTotal frame count [only effective for animated borders] (example -p:f=24)");
            Console.WriteLine("\t-p:s=\tAnimation speed [only effective for animated borders] (example -p:s=1)");
            Console.WriteLine("\t-p:in\tInvert animation direction [only effective for animated borders]");
            Console.WriteLine(new string('-', Console.WindowWidth));
            Console.WriteLine("Image manipulator - GermanBread#9077");
            return;

            #endregion

            #region Brightness normalisation

            Brighten:
            Console.WriteLine("==> Normalising brightness");
            
            try {
                image = Image.Load<Rgba32>(input);
            } catch {
                Console.WriteLine("Failed loading the image!");
                return;
            }
            
            // Figure out the highest brightness value
            int highestPixelValue = 0;
            Console.WriteLine("==> Analyzing image");
            Console.WriteLine("Analysing \"{0}\".", input);
            for (int row = 0; row < image.Height; row++)
            {
                for (int column = 0; column < image.Width; column++)
                {
                    // We want to figure out the highest value
                    if (image[column, row].R > highestPixelValue)
                        highestPixelValue = image[column, row].R;
                    if (image[column, row].G > highestPixelValue)
                        highestPixelValue = image[column, row].G;
                    if (image[column, row].B > highestPixelValue)
                        highestPixelValue = image[column, row].B;
                }
            }

            Console.WriteLine("Highest pixel value was \"{0}\".", highestPixelValue);
            Console.WriteLine("Pixel color values need to be increased by ~{0}%.", Math.Round(100f / (highestPixelValue / 255f)));
            
            Console.WriteLine("==> Normalising image");
            
            Console.WriteLine("Converting \"{0}\".", input);
            for (int row = 0; row < image.Height; row++)
            {
                for (int column = 0; column < image.Width; column++)
                {
                    // Get the pixel we're processing
                    Rgba32 _pixel = image[column, row];
                    
                    // Calculate the factor
                    float _factor = 1 / (highestPixelValue / 255f);
                    
                    // Adjust the Pixel
                    _pixel.R = (byte)Math.Round(_pixel.R * _factor);
                    _pixel.G = (byte)Math.Round(_pixel.G * _factor);
                    _pixel.B = (byte)Math.Round(_pixel.B * _factor);

                    // Now overwrite the original pixel with the new one
                    image[column, row] = _pixel;
                }
            }

            // Save output to disk
            Console.WriteLine("==> Saving modified image");
            image.SaveAsPng(output + ".png");
            Console.WriteLine("\"{0}\" got saved to \"{1}\"", input, output + ".png");
            
            goto Cleanup;

            #endregion

            #region Funkyfying

            Funkyfy:
            Console.WriteLine("==> Funkyfying image");
            
            try {
                image = Image.Load<Rgba32>(input);
            } catch {
                Console.WriteLine("Failed loading the image!");
                return;
            }
            conversionTasks = new List<Task<Rgba32[]>>();

            Console.WriteLine("Spawning tasks.", input);
            for (int row = 0; row < image.Height; row++)
            {
                conversionTasks.Add(Funkyfy(row));
            }
            
            Console.WriteLine("Converting {0}.", input);
            Task.WaitAll(conversionTasks.ToArray());

            Console.WriteLine("Merging results.", input);
            for (int row = 0; row < conversionTasks.Count; row++)
            {
                Rgba32[] conversionResult = conversionTasks[row].Result;
                for (int column = 0; column < conversionResult.Length; column++)
                {
                    image[column, row] = conversionResult[column];
                }
            }

            // Save output to disk
            Console.WriteLine("==> Saving modified image");
            image.SaveAsPng(output + ".png");
            Console.WriteLine("\"{0}\" got saved to \"{1}\"", input, output + ".png");
            
            goto Cleanup;

            async Task<Rgba32[]> Funkyfy(int row) {
                // Create an array for the output
                Rgba32[] output = new Rgba32[image.Width];
                
                // Modify the pixels
                for (int column = 0; column < image.Width; column++)
                {
                    // Get the pixel we're processing
                    Rgba32 _pixel = image[column, row];
                    
                    // Do more fancy stuff here
                    _pixel.R = (byte)Math.Pow(_pixel.R, 2);
                    _pixel.G = (byte)Math.Pow(_pixel.G, 2);
                    _pixel.B = (byte)Math.Pow(_pixel.B, 2);

                    // Now overwrite the original pixel with the new one
                    output[column] = _pixel;
                }
                
                // Return the result
                await Task.Delay(0);
                return output;
            }

            #endregion

            #region Border

            Border:
            Console.WriteLine("==> Adding border to image");
            
            try {
                image = Image.Load<Rgba32>(input);
            } catch {
                Console.WriteLine("Failed loading the image!");
                return;
            }
            conversionTasks = new List<Task<Rgba32[]>>();

            // Parse the color
            foreach (var arg in switches.Where(x => x.StartsWith("p:")).ToArray())
            {
                try {
                    if (arg.StartsWith("p:w=")) borderWidth = int.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse width parameter!");
                }
                try {
                    if (arg.StartsWith("p:r=")) innerBorderWidth = int.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse inner width parameter!");
                }
                try {
                    if (arg.StartsWith("p:c=")) borderColor = Rgba32.ParseHex(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse color parameter!");
                }
                try {
                    if (arg.StartsWith("p:i=")) borderImage = Image.Load<Rgba32>(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to load image!");
                }
                try {
                    if (arg.StartsWith("p:a=")) blurStrength = float.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed parse blur amount!");
                }
                try {
                    if (arg.StartsWith("p:b=")) backgroundDim = Math.Clamp(float.Parse(new string(arg.Skip(4).ToArray())), 0, 1);
                } catch {
                    Console.WriteLine("Failed to parse background-dim parameter!");
                }
            }
            borderCanvas = new Image<Rgba32>(image.Width + borderWidth * 2, image.Height + borderWidth * 2, new Rgba32(255, 0, 255, 255));

            // If a border image was specified, modify that image
            if (borderImage != null) {
                // Stretch the image to fit the screen
                Console.WriteLine("Resizing border image.");
                borderImage.Mutate(img => img.Resize(new Size(borderCanvas.Width, borderCanvas.Height)));
                
                // Apply a blur effect
                if (blurStrength > 0)
                    borderImage.Mutate(img => img.GaussianBlur(blurStrength));
            }

            Console.WriteLine("Spawning tasks.");
            for (int row = 0; row < borderCanvas.Height; row++)
            {
                conversionTasks.Add(AddBorder(row));
            }
            
            Console.WriteLine("Converting \"{0}\".", input);
            Task.WaitAll(conversionTasks.ToArray());

            Console.WriteLine("Merging results.");
            for (int row = 0; row < conversionTasks.Count; row++)
            {
                Rgba32[] conversionResult = conversionTasks[row].Result;
                for (int column = 0; column < conversionResult.Length; column++)
                {
                    borderCanvas[column, row] = conversionResult[column];
                }
            }

            // Save output to disk
            Console.WriteLine("==> Saving modified image");
            borderCanvas.SaveAsPng(output + ".png");
            Console.WriteLine("\"{0}\" got saved to \"{1}\"", input, output + ".png");

            goto Cleanup;

            async Task<Rgba32[]> AddBorder(int row) {
                // Create an array for the output
                Rgba32[] output = new Rgba32[borderCanvas.Width];
                
                // Modify the pixels
                for (int column = 0; column < borderCanvas.Width; column++) {
                    // Get the pixel we're processing
                    Rgba32 pixel = borderCanvas[column, row];

                    // Image border
                    if (borderImage != null) {
                        if ((column >= borderWidth - innerBorderWidth && column <= image.Width + borderWidth + innerBorderWidth) &&
                            (row >= borderWidth - innerBorderWidth && row <= image.Height + borderWidth + innerBorderWidth) && innerBorderWidth > 0) {
                            // This gets displayed on the border of the main image
                            pixel = borderImage[column, row];
                        }
                        else {
                            Rgba32 _imagePixel = borderImage[column, row];
                            byte _red = (byte)((_imagePixel.R / 255f) * 200f);
                            byte _green = (byte)((_imagePixel.G / 255f) * 200f);
                            byte _blue = (byte)((_imagePixel.B / 255f) * 200f);
                            // This is the color-rotating border, but a bit darker
                            pixel = new Rgba32(_red, _green, _blue, _imagePixel.A);
                        }
                    }
                    
                    // Color border
                    else if (borderColor != null) {
                        if ((column >= borderWidth - innerBorderWidth && column <= image.Width + borderWidth + innerBorderWidth) &&
                            (row >= borderWidth - innerBorderWidth && row <= image.Height + borderWidth + innerBorderWidth) && innerBorderWidth > 0) {
                            // This gets displayed on the border of the main image
                            pixel = borderColor.Value;
                        }
                        else {
                            byte _red = (byte)((borderColor.Value.R / 255f) * 200f);
                            byte _green = (byte)((borderColor.Value.G / 255f) * 200f);
                            byte _blue = (byte)((borderColor.Value.B / 255f) * 200f);
                            // This is a bit darker
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                    }

                    // If no border color was provided
                    else {
                        if ((column >= borderWidth - innerBorderWidth && column <= image.Width + borderWidth + innerBorderWidth) &&
                            (row >= borderWidth - innerBorderWidth && row <= image.Height + borderWidth + innerBorderWidth)) {
                            byte _red = (byte)Math.Abs(Math.Sin(rgbOffset + (float)column / borderCanvas.Width * 3) * 255);
                            byte _green = (byte)Math.Abs(Math.Sin(rgbOffset + (float)column / borderCanvas.Width * 3 - 45) * 255);
                            byte _blue = (byte)Math.Abs(Math.Sin(rgbOffset + (float)column / borderCanvas.Width * 3 - 90) * 255);
                            // This gets displayed on the border of the main image
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                        else {
                            byte _red = (byte)Math.Abs(Math.Sin(rgbOffset + (float)column / borderCanvas.Width * 3) * 100);
                            byte _green = (byte)Math.Abs(Math.Sin(rgbOffset + (float)column / borderCanvas.Width * 3 - 45) * 100);
                            byte _blue = (byte)Math.Abs(Math.Sin(rgbOffset + (float)column / borderCanvas.Width * 3 - 90) * 100);
                            // This is the color-rotating border, but a bit darker
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                    }

                    // Copy the pixel from the original image
                    if ((column > borderWidth && column < image.Width + borderWidth) &&
                        (row > borderWidth && row < image.Height + borderWidth)) {
                        // Get the pixel of the image
                        Rgba32 imagePixel = image[column - borderWidth, row - borderWidth];
                        // Get the pixel of the border image
                        Rgba32 borderImagePixel;
                        if (borderImage != null)
                            borderImagePixel = borderImage[column, row];
                        else
                            // Fallback if the border image was not specified
                            borderImagePixel = pixel;
                        // Do some blending magic
                        pixel = new Rgba32(
                            (imagePixel.R / 255f) * (imagePixel.A / 255f) + (borderImagePixel.R / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            (imagePixel.G / 255f) * (imagePixel.A / 255f) + (borderImagePixel.G / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            (imagePixel.B / 255f) * (imagePixel.A / 255f) + (borderImagePixel.B / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            1
                        );
                    }

                    // Now overwrite result to the array
                    output[column] = pixel;
                }
                
                // Return the result
                await Task.Delay(0);
                return output;
            }

            #endregion

            #region CircularBorder

            CircularBorder:
            Console.WriteLine("==> Adding border to image");
            
            conversionTasks = new List<Task<Rgba32[]>>();

            // Parse the color
            foreach (var arg in switches.Where(x => x.StartsWith("p:")).ToArray())
            {
                try {
                    if (arg.StartsWith("p:w=")) borderWidth = int.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse width parameter!");
                }
                try {
                    if (arg.StartsWith("p:r=")) innerBorderWidth = int.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse inner width parameter!");
                }
                try {
                    if (arg.StartsWith("p:c=")) borderColor = Rgba32.ParseHex(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse color parameter!");
                }
                try {
                    if (arg.StartsWith("p:i=")) borderImage = Image.Load<Rgba32>(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to load image!");
                }
                try {
                    if (arg.StartsWith("p:a=")) blurStrength = float.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed parse blur amount!");
                }
                try {
                    if (arg.StartsWith("p:b=")) backgroundDim = Math.Clamp(float.Parse(new string(arg.Skip(4).ToArray())), 0, 1);
                } catch {
                    Console.WriteLine("Failed to parse background-dim parameter!");
                }
                if (arg.StartsWith("p:d")) discardPixels = true;
                if (arg.StartsWith("p:an")) animated = true;
                try {
                    if (arg.StartsWith("p:f=")) frameCount = int.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse frame-count parameter!");
                }
                try {
                    if (arg.StartsWith("p:s=")) animationSpeed = int.Parse(new string(arg.Skip(4).ToArray()));
                } catch {
                    Console.WriteLine("Failed to parse animation speed parameter!");
                }
                if (arg.StartsWith("p:in")) reversedAnimation = true;
            }
            try {
                image = Image.Load<Rgba32>(input);
                if (animated) image.Mutate(x => x.Resize(new Size(1024)));
            } catch {
                Console.WriteLine("Failed loading the image!");
                return;
            }
            
            borderCanvas = new Image<Rgba32>(image.Width + borderWidth * 2, image.Height + borderWidth * 2, new Rgba32(255, 0, 255, 255));

            // If a border image was specified, modify that image
            if (borderImage != null) {
                // Stretch the image to fit the screen
                Console.WriteLine("Resizing border image.");
                borderImage.Mutate(img => img.Resize(new Size(borderCanvas.Width, borderCanvas.Height)));
                
                // Apply a blur effect
                if (blurStrength > 0)
                    borderImage.Mutate(img => img.GaussianBlur(blurStrength));
            }

            
            // If the border is not animated, fall through
            async Task SpawnTasks() {
                if (animated) goto Animated;
                Console.WriteLine("Spawning tasks.");
                for (int row = 0; row < borderCanvas.Height; row++)
                {
                    Task<Rgba32[]> tsk = AddCircularBorder(row);
                    conversionTasks.Add(tsk);
                }
                // Skip the animation part
                await Task.Delay(0);
                return;
                
                Animated:
                for (float i = 0; i < frameCount; i++)
                {
                    Console.WriteLine("Spawning tasks for frame {0}.", i + 1);
                    for (int row = 0; row < borderCanvas.Height; row++)
                    {
                        float rot = (MathF.Abs(i / frameCount - (reversedAnimation ? 1 : 0)) * 180f) / (180f / MathF.PI);
                        Task<Rgba32[]> tsk = AddCircularBorderFrame(row, rot);
                        conversionTasks.Add(tsk);
                    }
                }

                await Task.Delay(0);
                return;
            }

            SpawnTasks().Wait();
            
            Console.WriteLine("Waiting for conversion tasks to finish");
            Task.WaitAll(conversionTasks.ToArray());

            Console.WriteLine("Merging results.");
            if (animated) goto AnimatedMerge;
            for (int row = 0; row < conversionTasks.Count; row++)
            {
                Rgba32[] conversionResult = conversionTasks[row].Result;
                for (int column = 0; column < conversionResult.Length; column++)
                {
                    borderCanvas[column, row] = conversionResult[column];
                }
            }
            goto SavePng;

            AnimatedMerge:
            // Before we do anything, resize the canvas
            gifCanvas = new Image<Rgba32>(borderCanvas.Width, borderCanvas.Height);
            for (int frame = 0; frame < frameCount; frame++)
            {
                Console.WriteLine("Merging frame {0} out of {1}.", frame + 1, frameCount);
                Image<Rgba32> gifFrame = new Image<Rgba32>(borderCanvas.Width, borderCanvas.Height);
                for (int row = 0; row < conversionTasks.Count / frameCount; row++)
                {
                    Rgba32[] conversionResult = conversionTasks[row + frame * (conversionTasks.Count / frameCount)].Result;
                    for (int column = 0; column < conversionResult.Length; column++)
                    {
                        gifFrame[column, row] = conversionResult[column];
                    }
                }
                gifFrame.Mutate(x => x.Resize(new Size(gifCanvas.Width, gifCanvas.Height)));
                gifCanvas.Frames.AddFrame(gifFrame.Frames[0]);
                gifFrame.Dispose();
            }

            // Remove the first, invisible frame
            gifCanvas.Frames.RemoveFrame(0);

            // Save output to disk
            Console.WriteLine("==> Saving animated image");
            
            gifCanvas.Metadata.GetGifMetadata().RepeatCount = 0;
            gifCanvas.SaveAsGif(output + ".gif");
            Console.WriteLine("\"{0}\" got saved to \"{1}\"", input, output + ".gif");

            goto Cleanup;

            SavePng:
            // Save output to disk
            Console.WriteLine("==> Saving modified image");
            borderCanvas.SaveAsPng(output + ".png");
            Console.WriteLine("\"{0}\" got saved to \"{1}\"", input, output + ".png");

            goto Cleanup;

            async Task<Rgba32[]> AddCircularBorder(int row) {
                // Create an array for the output
                Rgba32[] output = new Rgba32[borderCanvas.Width];
                float maxDistanceToCenter = borderCanvas.Width / 2;
                
                // Modify the pixels
                for (int column = 0; column < borderCanvas.Width; column++) {
                    float distanceToCenter = Vector2.Distance(new Vector2(column, row), new Vector2(borderCanvas.Width / 2f, borderCanvas.Height / 2f));
                    Vector2 directionVector = Vector2.Normalize(Vector2.Subtract(new Vector2(column, row), new Vector2(borderCanvas.Width / 2f, borderCanvas.Height / 2f)));
                    float angleFromTop = MathF.Atan2(directionVector.Y, directionVector.X);
                    
                    // Get the pixel we're processing
                    Rgba32 pixel = borderCanvas[column, row];

                    // If the pixel is too far away, we kcan skip this code and just set the pixel in question transparent
                    if (distanceToCenter > maxDistanceToCenter && discardPixels) {
                        pixel = new Rgba32(0, 0, 0, 0);
                        // We don't want to waste processing time, do we?
                        goto SkipBorders;
                    }

                    // Image border
                    if (borderImage != null) {
                        if (distanceToCenter + (borderWidth - innerBorderWidth) < maxDistanceToCenter && innerBorderWidth > 0) {
                            // This gets displayed on the border of the main image
                            pixel = borderImage[column, row];
                        }
                        else {
                            Rgba32 _imagePixel = borderImage[column, row];
                            byte _red = (byte)((_imagePixel.R / 255f) * 200f);
                            byte _green = (byte)((_imagePixel.G / 255f) * 200f);
                            byte _blue = (byte)((_imagePixel.B / 255f) * 200f);
                            pixel = new Rgba32(_red, _green, _blue, _imagePixel.A);
                        }
                    }
                    
                    // Color border
                    else if (borderColor != null) {
                        if (distanceToCenter + (borderWidth - innerBorderWidth) < maxDistanceToCenter && innerBorderWidth > 0) {
                            // This gets displayed on the border of the main image
                            pixel = borderColor.Value;
                        }
                        else {
                            byte _red = (byte)((borderColor.Value.R / 255f) * 200f);
                            byte _green = (byte)((borderColor.Value.G / 255f) * 200f);
                            byte _blue = (byte)((borderColor.Value.B / 255f) * 200f);
                            // This is a bit darker
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                    }

                    // If no border color was provided
                    else {
                        if (distanceToCenter + (borderWidth - innerBorderWidth) < maxDistanceToCenter && innerBorderWidth > 0) {
                            byte _red = (byte)Math.Abs(Math.Sin(rgbOffset + angleFromTop / 2) * 255);
                            byte _green = (byte)Math.Abs(Math.Sin(rgbOffset + angleFromTop / 2 - 45) * 255);
                            byte _blue = (byte)Math.Abs(Math.Sin(rgbOffset + angleFromTop / 2 - 90) * 255);
                            // This gets displayed on the border of the main image
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                        else {
                            byte _red = (byte)Math.Abs(Math.Sin(rgbOffset + angleFromTop / 2) * 100);
                            byte _green = (byte)Math.Abs(Math.Sin(rgbOffset + angleFromTop / 2 - 45) * 100);
                            byte _blue = (byte)Math.Abs(Math.Sin(rgbOffset + angleFromTop / 2 - 90) * 100);
                            // This is the color-rotating border, but a bit darker
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                    }

                    // To avoid some spaghetti
                    SkipBorders:

                    // Copy the pixel from the original image
                    if (distanceToCenter < image.Width / 2) {
                        // Get the pixel of the image
                        Rgba32 imagePixel = image[column - borderWidth, row - borderWidth];
                        // Get the pixel of the border image
                        Rgba32 borderImagePixel;
                        if (borderImage != null)
                            borderImagePixel = borderImage[column, row];
                        else
                            // Fallback if the border image was not specified
                            borderImagePixel = pixel;
                        // Do some blending magic
                        pixel = new Rgba32(
                            (imagePixel.R / 255f) * (imagePixel.A / 255f) + (borderImagePixel.R / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            (imagePixel.G / 255f) * (imagePixel.A / 255f) + (borderImagePixel.G / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            (imagePixel.B / 255f) * (imagePixel.A / 255f) + (borderImagePixel.B / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            1
                        );
                    }

                    // Now overwrite result to the array
                    output[column] = pixel;
                }
                
                // Return the result
                await Task.Delay(0);
                return output;
            }


            async Task<Rgba32[]> AddCircularBorderFrame(int row, float frame) {
                // Create an array for the output
                Rgba32[] output = new Rgba32[borderCanvas.Width];
                float maxDistanceToCenter = borderCanvas.Width / 2;
                
                // Modify the pixels
                for (int column = 0; column < borderCanvas.Width; column++) {
                    float distanceToCenter = Vector2.Distance(new Vector2(column, row), new Vector2(borderCanvas.Width / 2f, borderCanvas.Height / 2f));
                    Vector2 directionVector = Vector2.Normalize(Vector2.Subtract(new Vector2(column, row), new Vector2(borderCanvas.Width / 2f, borderCanvas.Height / 2f)));
                    float angleFromTop = MathF.Atan2(directionVector.Y, directionVector.X);
                    
                    // Get the pixel we're processing
                    Rgba32 pixel = borderCanvas[column, row];

                    // If the pixel is too far away, we kcan skip this code and just set the pixel in question transparent
                    if (distanceToCenter > maxDistanceToCenter && discardPixels) {
                        pixel = new Rgba32(0, 0, 0, 0);
                        // We don't want to waste processing time, do we?
                        goto SkipBorders;
                    }

                    // Image border
                    if (borderImage != null) {
                        if (distanceToCenter + (borderWidth - innerBorderWidth) < maxDistanceToCenter && innerBorderWidth > 0) {
                            // This gets displayed on the border of the main image
                            pixel = borderImage[column, row];
                        }
                        else {
                            Rgba32 _imagePixel = borderImage[column, row];
                            byte _red = (byte)((_imagePixel.R / 255f) * 200f);
                            byte _green = (byte)((_imagePixel.G / 255f) * 200f);
                            byte _blue = (byte)((_imagePixel.B / 255f) * 200f);
                            pixel = new Rgba32(_red, _green, _blue, _imagePixel.A);
                        }
                    }
                    
                    // Color border
                    else if (borderColor != null) {
                        if (distanceToCenter + (borderWidth - innerBorderWidth) < maxDistanceToCenter && innerBorderWidth > 0) {
                            // This gets displayed on the border of the main image
                            pixel = borderColor.Value;
                        }
                        else {
                            byte _red = (byte)((borderColor.Value.R / 255f) * 200f);
                            byte _green = (byte)((borderColor.Value.G / 255f) * 200f);
                            byte _blue = (byte)((borderColor.Value.B / 255f) * 200f);
                            // This is a bit darker
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                    }

                    // If no border color was provided
                    else {
                        if (distanceToCenter + (borderWidth - innerBorderWidth) < maxDistanceToCenter && innerBorderWidth > 0) {
                            byte _red = (byte)Math.Abs(Math.Sin(frame + rgbOffset + angleFromTop / 2) * 255);
                            byte _green = (byte)Math.Abs(Math.Sin(frame + rgbOffset + angleFromTop / 2 - 45) * 255);
                            byte _blue = (byte)Math.Abs(Math.Sin(frame + rgbOffset + angleFromTop / 2 - 90) * 255);
                            // This gets displayed on the border of the main image
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                        else {
                            byte _red = (byte)Math.Abs(Math.Sin(frame + rgbOffset + angleFromTop / 2) * 100);
                            byte _green = (byte)Math.Abs(Math.Sin(frame + rgbOffset + angleFromTop / 2 - 45) * 100);
                            byte _blue = (byte)Math.Abs(Math.Sin(frame + rgbOffset + angleFromTop / 2 - 90) * 100);
                            // This is the color-rotating border, but a bit darker
                            pixel = new Rgba32(_red, _green, _blue, 255);
                        }
                    }

                    // To avoid some spaghetti
                    SkipBorders:

                    // Copy the pixel from the original image
                    if (distanceToCenter < image.Width / 2) {
                        // Get the pixel of the image
                        Rgba32 imagePixel;
                        try {
                            imagePixel = image[column - borderWidth, row - borderWidth];
                        } catch {
                            imagePixel = pixel;
                        }
                        // Get the pixel of the border image
                        Rgba32 borderImagePixel;
                        if (borderImage != null)
                            borderImagePixel = borderImage[column, row];
                        else
                            // Fallback if the border image was not specified
                            borderImagePixel = pixel;
                        // Do some blending magic
                        pixel = new Rgba32(
                            (imagePixel.R / 255f) * (imagePixel.A / 255f) + (borderImagePixel.R / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            (imagePixel.G / 255f) * (imagePixel.A / 255f) + (borderImagePixel.G / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            (imagePixel.B / 255f) * (imagePixel.A / 255f) + (borderImagePixel.B / 255f) * (1 - imagePixel.A / 255f) * backgroundDim, 
                            1
                        );
                    }

                    // Now overwrite result to the array
                    output[column] = pixel;
                }
                
                // Return the result
                await Task.Delay(0);
                return output;
            }

            #endregion

            #region Memory cleanup code

            Cleanup:
            // Dispose the image we've processed, we won't need it anymore, this value cannot be null
            image.Dispose();
            // Check if these two values are not null, then dispose them
            if (borderImage != null) borderImage.Dispose(); // Value is not null when a image parameter has been provided
            if (borderCanvas != null) borderCanvas.Dispose(); // Value is not null when a border has been added to the image
            if (gifCanvas != null) gifCanvas.Dispose(); // Value is not null when a gif has been generated
            // We want to free up the memory used by each Task (thousands get created and they take memory)
            if (conversionTasks == null) return;
            foreach (Task completedTask in conversionTasks) {
                completedTask.Dispose();
            }
            return;

            #endregion
        }
    }
}
