# frozen_string_literal: true
#
# Comparable implemented in Ruby on top of <=>.

module Comparable
  def ==(other)
    c = (self <=> other)
    !c.nil? && c == 0
  end

  def <(other)
    cmp(other) < 0
  end

  def <=(other)
    cmp(other) <= 0
  end

  def >(other)
    cmp(other) > 0
  end

  def >=(other)
    cmp(other) >= 0
  end

  def between?(min, max)
    self >= min && self <= max
  end

  def clamp(min, max = nil)
    if max.nil? && min.is_a?(Range)
      lo = min.begin
      hi = min.end
    else
      lo = min
      hi = max
    end
    return lo if !lo.nil? && self < lo
    return hi if !hi.nil? && self > hi
    self
  end

  def cmp(other)
    c = (self <=> other)
    raise ArgumentError, "comparison of #{self.class} with #{other.class} failed" if c.nil?
    c
  end
end
