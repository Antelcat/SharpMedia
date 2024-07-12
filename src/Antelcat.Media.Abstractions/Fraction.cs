namespace Antelcat.Media.Abstractions;

/// <summary>
/// 代表一个正分数，由分子和分母组成
/// </summary>
public readonly struct Fraction {
	public uint Number { get; } = 1;

	public uint Denominator { get; } = 1;

	public Fraction(uint number, uint denominator) {
		if (denominator == 0) {
			throw new ArgumentOutOfRangeException(nameof(denominator));
		}
		Number = number;
		Denominator = denominator;
	}

	public static implicit operator Fraction(uint fps) {
		return new Fraction(fps, 1);
	}

	public double ToDouble() {
		return (double)Number / Denominator;
	}

	public override string ToString() {
		return $"{Number}/{Denominator}={ToDouble()}";
	}

	public override bool Equals(object? obj) {
		return obj is Fraction other && Equals(other);
	}

	public override int GetHashCode() {
		return HashCode.Combine(Number.GetHashCode(), Denominator.GetHashCode());
	}

	public bool Equals(Fraction other) {
		return Number == other.Number && Denominator == other.Denominator;
	}
	
	public static bool operator ==(Fraction left, Fraction right) {
		return left.Equals(right);
	}
	
	public static bool operator !=(Fraction left, Fraction right) {
		return !left.Equals(right);
	}
}