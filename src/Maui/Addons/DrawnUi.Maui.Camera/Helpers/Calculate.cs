namespace DrawnUi.Controls
{


	public static class Calculate
	{
		public static double InchesToMillimeters(double value)
		{
			return value * ConvertInchesToMillimetersRatio;
		}

		public static double MillimetersToInches(double value)
		{
			return value / ConvertInchesToMillimetersRatio;
		}

		public static double ConvertInchesToMillimetersRatio = 25.4;

		public static double CropFactor35mm = 43.266615305567875;

		public static double Diagonal(double width, double height)
		{
			return Math.Sqrt((width * width) + (height * height));
		}

		/// <summary>
		/// Crop-factor, params in mm
		/// <see href="https://ru.wikipedia.org/wiki/%D0%9A%D1%80%D0%BE%D0%BF-%D1%84%D0%B0%D0%BA%D1%82%D0%BE%D1%80">More info..</see>
		/// </summary>
		public static double CropFactor(double width, double height)
		{
			//K_f = диагональ35мм / диагональматрица (диагональ малоформатного кадра 24×36 мм ≈ 43,3 мм)
			var ret = CropFactor35mm / Diagonal(width, height);
			return ret;
		}

		/// <summary>
		/// Crop-factor, params in mm
		/// <see href="https://ru.wikipedia.org/wiki/%D0%9A%D1%80%D0%BE%D0%BF-%D1%84%D0%B0%D0%BA%D1%82%D0%BE%D1%80">More info..</see>
		/// </summary>
		public static double CropFactor(double diagonal)
		{
			//K_f = диагональ35мм / диагональматрица(диагональ малоформатного кадра 24×36 мм ≈ 43, 3 мм)
			var ret = CropFactor35mm / diagonal;
			return ret;
		}


	}
}
