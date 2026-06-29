#pragma once
#include <stdint.h>

struct Fixed
{
	int64_t raw;

	static constexpr int SHIFT = 16;
	static constexpr int64_t ONE = (int64_t)1 << SHIFT; // = 65536 = 1.0

	static Fixed FromInt(int32_t v) { return Fixed{ (int64_t)v << SHIFT }; }
	static Fixed FromRaw(int64_t r) { return Fixed{ r }; }

	int32_t ToInt() const { return (int32_t)(raw >> SHIFT); }
	float ToFloat() const { return (float)raw / (float)ONE; }
};

inline Fixed operator+(Fixed a, Fixed b) { return Fixed{ a.raw + b.raw }; }
inline Fixed operator-(Fixed a, Fixed b) { return Fixed{ a.raw - b.raw }; }
inline Fixed operator*(Fixed a, Fixed b) { return Fixed{ (a.raw * b.raw) >> Fixed::SHIFT }; }
inline Fixed operator/(Fixed a, Fixed b) { return Fixed{ (a.raw << Fixed::SHIFT) / b.raw }; }